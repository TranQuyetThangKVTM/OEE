using Oee.Application.Contracts.Identity;

namespace Oee.Application.Identity;

/// <summary>Cổng truy xuất tài khoản và API mật khẩu/role; không lộ UserManager, IdentityResult hay entity Identity.</summary>
public interface IIdentityAccountGateway
{
    Task<IdentityAccountPage> SearchAsync(
        UserSearchRequest request,
        DateTimeOffset now,
        CancellationToken ct);

    Task<ManagedUserAccount?> FindAsync(Guid id);

    Task<IReadOnlyCollection<UserRoleResponse>> ReadRolesAsync(CancellationToken ct);

    Task<bool> EmailUsedAsync(
        string email,
        Guid? excludedId,
        CancellationToken ct);

    Task<int> CountActiveAdminsAsync(DateTimeOffset now, CancellationToken ct);

    Task<IReadOnlyCollection<string>> GetRolesAsync(ManagedUserAccount user);

    Task<bool> IsInRoleAsync(ManagedUserAccount user, string role);

    Task<AccountOperationResult> CreateAsync(ManagedUserAccount user, string password);

    Task<AccountOperationResult> UpdateAsync(ManagedUserAccount user);

    Task<AccountOperationResult> AddToRolesAsync(ManagedUserAccount user, IEnumerable<string> roles);

    Task<AccountOperationResult> RemoveFromRolesAsync(
        ManagedUserAccount user,
        IEnumerable<string> roles);

    Task<string> GeneratePasswordResetTokenAsync(ManagedUserAccount user);

    Task<AccountOperationResult> ResetPasswordAsync(
        ManagedUserAccount user,
        string token,
        string password);

    Task<AccountOperationResult> ResetAccessFailedCountAsync(ManagedUserAccount user);

    Task<AccountOperationResult> ChangePasswordAsync(
        ManagedUserAccount user,
        string currentPassword,
        string newPassword);
}
