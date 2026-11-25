using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Oee.Application.Common;
using Oee.Application.Contracts.Identity;
using Oee.Application.Identity;
using Oee.Application.Security;
using Oee.Infrastructure.Persistence;

namespace Oee.Infrastructure.Identity;

/// <summary>Adapter ASP.NET Identity; mọi thao tác dùng cùng DbContext với đơn vị ghi/audit.</summary>
public sealed class IdentityAccountGateway(UserManager<ApplicationUser> userManager, EfApplicationUnitOfWork unitOfWork) : IIdentityAccountGateway
{
    private OeeDbContext dbContext => unitOfWork.Db;
    /// <summary>Tìm user theo username/email/display name/role/state và paging tại SQL.</summary>
    public async Task<IdentityAccountPage> SearchAsync(
        UserSearchRequest request, DateTimeOffset now,
        CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var query = userManager.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(user =>
                (user.UserName != null && user.UserName.Contains(search))
                || (user.Email != null && user.Email.Contains(search))
                || (user.DisplayName != null && user.DisplayName.Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            var role = request.Role.Trim();
            query = query.Where(user =>
                dbContext.UserRoles.Any(userRole =>
                    userRole.UserId == user.Id
                    && dbContext.Roles.Any(identityRole =>
                        identityRole.Id == userRole.RoleId && identityRole.Name == role)));
        }

        query = request.State switch
        {
            "Active" => query.Where(user => !user.IsDisabled && (user.LockoutEnd == null || user.LockoutEnd <= now)),
            "Locked" => query.Where(user => !user.IsDisabled && user.LockoutEnd > now),
            "Disabled" => query.Where(user => user.IsDisabled),
            _ => query
        };

        var totalCount = await query.CountAsync(ct);
        query = (request.SortBy, request.Descending) switch
        {
            ("displayName", true) => query.OrderByDescending(user => user.DisplayName),
            ("displayName", false) => query.OrderBy(user => user.DisplayName),
            ("email", true) => query.OrderByDescending(user => user.Email),
            ("email", false) => query.OrderBy(user => user.Email),
            ("status", true) => query.OrderByDescending(user => user.IsDisabled).ThenByDescending(user => user.LockoutEnd),
            ("status", false) => query.OrderBy(user => user.IsDisabled).ThenBy(user => user.LockoutEnd),
            (_, true) => query.OrderByDescending(user => user.UserName),
            _ => query.OrderBy(user => user.UserName)
        };

        var users = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(ct);
        // Một join theo page user lấy toàn bộ role, tránh gọi GetRolesAsync theo từng dòng (N+1).
        var userIds = users.Select(user => user.Id).ToArray();
        var roleRows = await (
            from userRole in dbContext.UserRoles.AsNoTracking()
            join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where userIds.Contains(userRole.UserId)
            select new { userRole.UserId, Role = role.Name! })
            .ToArrayAsync(ct);
        var rolesByUser = roleRows
            .GroupBy(row => row.UserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<string>)group.Select(row => row.Role).Order().ToArray());
        return new IdentityAccountPage(users.Select(ToAccount).ToArray(), rolesByUser, totalCount);
    }
    public async Task<ManagedUserAccount?> FindAsync(Guid id)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        return user is null ? null : ToAccount(user);
    }
    public async Task<IReadOnlyCollection<UserRoleResponse>> ReadRolesAsync(CancellationToken ct) =>
        await dbContext.Roles.AsNoTracking().Where(role => role.Name != null).OrderBy(role => role.Name)
            .Select(role => new UserRoleResponse(role.Id, role.Name!)).ToArrayAsync(ct);
    public Task<bool> EmailUsedAsync(string email, Guid? excludedId, CancellationToken ct)
    {
        var normalized = userManager.NormalizeEmail(email);
        return userManager.Users.AsNoTracking().AnyAsync(user => user.Id != excludedId && user.NormalizedEmail == normalized, ct);
    }
    public Task<int> CountActiveAdminsAsync(DateTimeOffset now, CancellationToken ct) =>
        (from user in dbContext.Users
         join userRole in dbContext.UserRoles on user.Id equals userRole.UserId
         join role in dbContext.Roles on userRole.RoleId equals role.Id
         where role.Name == RoleCapabilityMatrix.Admin && !user.IsDisabled && (user.LockoutEnd == null || user.LockoutEnd <= now)
         select user.Id).Distinct().CountAsync(ct);
    public async Task<IReadOnlyCollection<string>> GetRolesAsync(ManagedUserAccount user) =>
        (await userManager.GetRolesAsync(await RequiredUserAsync(user.Id))).ToArray();
    public async Task<bool> IsInRoleAsync(ManagedUserAccount user, string role) => await userManager.IsInRoleAsync(await RequiredUserAsync(user.Id), role);
    public async Task<AccountOperationResult> CreateAsync(ManagedUserAccount user, string password)
    {
        var entity = new ApplicationUser { Id = user.Id };
        ApplyProfile(entity, user);
        var result = await userManager.CreateAsync(entity, password);
        Refresh(user, entity);
        return ToResult(result);
    }
    public Task<AccountOperationResult> UpdateAsync(ManagedUserAccount user) => ExecuteAsync(user, entity => userManager.UpdateAsync(entity), true);
    public Task<AccountOperationResult> AddToRolesAsync(ManagedUserAccount user, IEnumerable<string> roles) => ExecuteAsync(user, entity => userManager.AddToRolesAsync(entity, roles));
    public Task<AccountOperationResult> RemoveFromRolesAsync(ManagedUserAccount user, IEnumerable<string> roles) => ExecuteAsync(user, entity => userManager.RemoveFromRolesAsync(entity, roles));
    public async Task<string> GeneratePasswordResetTokenAsync(ManagedUserAccount user) => await userManager.GeneratePasswordResetTokenAsync(await RequiredUserAsync(user.Id));
    public Task<AccountOperationResult> ResetPasswordAsync(ManagedUserAccount user, string token, string password) => ExecuteAsync(user, entity => userManager.ResetPasswordAsync(entity, token, password));
    public Task<AccountOperationResult> ResetAccessFailedCountAsync(ManagedUserAccount user) => ExecuteAsync(user, entity => userManager.ResetAccessFailedCountAsync(entity));
    public Task<AccountOperationResult> ChangePasswordAsync(ManagedUserAccount user, string currentPassword, string newPassword) => ExecuteAsync(user, entity => userManager.ChangePasswordAsync(entity, currentPassword, newPassword));

    private async Task<AccountOperationResult> ExecuteAsync(ManagedUserAccount user, Func<ApplicationUser, Task<IdentityResult>> operation, bool applyProfile = false)
    {
        var entity = await RequiredUserAsync(user.Id);
        if (applyProfile) ApplyProfile(entity, user);
        try
        {
            var result = await operation(entity);
            Refresh(user, entity);
            return ToResult(result);
        }
        catch (DbUpdateConcurrencyException exception) { throw new ConcurrencyConflictException("The user changed after it was loaded.", exception); }
        catch (DbUpdateException exception) { throw new PersistenceWriteException("The user could not be saved.", exception); }
    }
    private async Task<ApplicationUser> RequiredUserAsync(Guid id) => await userManager.FindByIdAsync(id.ToString()) ?? throw new KeyNotFoundException($"User '{id}' was not found.");
    private static AccountOperationResult ToResult(IdentityResult result) => new(result.Succeeded, result.Errors.Select(error => error.Description).ToArray());
    private static ManagedUserAccount ToAccount(ApplicationUser entity)
    {
        var account = new ManagedUserAccount();
        Refresh(account, entity);
        return account;
    }
    private static void Refresh(ManagedUserAccount account, ApplicationUser entity)
    {
        account.Id = entity.Id; account.UserName = entity.UserName; account.DisplayName = entity.DisplayName;
        account.Email = entity.Email; account.EmailConfirmed = entity.EmailConfirmed;
        account.LockoutEnabled = entity.LockoutEnabled; account.LockoutEnd = entity.LockoutEnd;
        account.IsDisabled = entity.IsDisabled;
        account.ConcurrencyStamp = entity.ConcurrencyStamp;
    }
    private static void ApplyProfile(ApplicationUser entity, ManagedUserAccount account)
    {
        entity.UserName = account.UserName; entity.DisplayName = account.DisplayName; entity.Email = account.Email;
        entity.EmailConfirmed = account.EmailConfirmed; entity.LockoutEnabled = account.LockoutEnabled;
        entity.LockoutEnd = account.LockoutEnd; entity.IsDisabled = account.IsDisabled;
    }
}
