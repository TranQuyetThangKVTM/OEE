using System.Data;
using System.Text.Json;
using Oee.Application.Audit;
using Oee.Application.Common;
using Oee.Application.Contracts.Identity;
using Oee.Application.Security;
using Oee.Domain.Platform;

namespace Oee.Application.Identity.Users.Common;

/// <summary>Dependency và quy tắc dùng chung của các thao tác UserManagement.</summary>
internal sealed class UserManagementContext(
    IIdentityAccountGateway accounts,
    IApplicationUnitOfWork unitOfWork,
    ICurrentActor currentActor,
    IClock clock,
    ICapabilityChecker? capabilityChecker = null)
    : IUseCaseContext
{
    internal IIdentityAccountGateway Accounts => accounts;
    internal IApplicationUnitOfWork UnitOfWork => unitOfWork;
    internal ICurrentActor CurrentActor => currentActor;
    internal IClock Clock => clock;

    internal async Task<ManagedUserAccount> GetUserAsync(Guid userId)
        => await accounts.FindAsync(userId) ?? throw new KeyNotFoundException($"User '{userId}' was not found.");

    internal UserResponse Map(
        ManagedUserAccount user,
        IReadOnlyCollection<string> roles)
        => new(
            user.Id,
            user.UserName ?? string.Empty,
            user.DisplayName,
            user.Email,
            user.LockoutEnd > clock.UtcNow,
            user.IsDisabled,
            roles,
            user.ConcurrencyStamp ?? string.Empty);

    internal async Task ValidateRolesAsync(
        IEnumerable<string> roles,
        CancellationToken cancellationToken)
    {
        var roleArray = roles.ToArray();

        if (roleArray.Length == 0)
        {
            throw new ApplicationRuleException("At least one role is required.");
        }

        var existingRoles = (await accounts.ReadRolesAsync(cancellationToken)).Select(role => role.Name).ToArray();
        var invalidRoles = roleArray.Except(existingRoles, StringComparer.Ordinal).ToArray();

        if (invalidRoles.Length > 0)
        {
            throw new ApplicationRuleException($"Unknown roles: {string.Join(", ", invalidRoles)}.");
        }
    }

    internal static string[] DistinctRoles(IEnumerable<string> roles)
        => roles.Distinct(StringComparer.Ordinal).Order().ToArray();

    internal async Task EnsureEmailIsUniqueAsync(
        string? email,
        Guid? exceptUserId,
        CancellationToken cancellationToken)
    {
        var normalized = NormalizeOptional(email);

        if (normalized is not null && await accounts.EmailUsedAsync(normalized, exceptUserId, cancellationToken))
            throw new ApplicationRuleException("Email is already used by another account.");
    }

    internal static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    internal void EnsureAuthorized()
    {
        if (capabilityChecker is not null && !capabilityChecker.HasCapability(Capabilities.UsersManage))
        {
            throw new UnauthorizedAccessException("The users.manage capability is required.");
        }
    }

    internal async Task ReplaceRolesAsync(
        ManagedUserAccount user,
        IEnumerable<string> currentRoles,
        IEnumerable<string> desiredRoles)
    {
        var current = currentRoles.ToArray();
        var desired = desiredRoles.ToArray();
        EnsureSucceeded(
            await accounts.RemoveFromRolesAsync(user, current.Except(desired, StringComparer.Ordinal)),
            "remove roles");
        EnsureSucceeded(
            await accounts.AddToRolesAsync(user, desired.Except(current, StringComparer.Ordinal)),
            "add roles");
    }

    internal async Task EnsureAdminRemainsActiveAsync(
        IEnumerable<string> currentRoles,
        IEnumerable<string> desiredRoles,
        CancellationToken cancellationToken)
    {
        if (currentRoles.Contains(RoleCapabilityMatrix.Admin, StringComparer.Ordinal)
            && !desiredRoles.Contains(RoleCapabilityMatrix.Admin, StringComparer.Ordinal))
        {
            await EnsureNotLastActiveAdminAsync(cancellationToken);
        }
    }

    internal bool IsSelf(Guid userId) => Guid.TryParse(currentActor.UserId, out var actorId) && actorId == userId;

    internal void EnsureNotSelf(Guid userId, string operation)
    {
        if (IsSelf(userId))
        {
            throw new ApplicationRuleException($"An administrator cannot {operation} their own account.");
        }
    }

    internal async Task EnsureNotLastActiveAdminAsync(CancellationToken cancellationToken)
    {
        var activeAdminCount = await accounts.CountActiveAdminsAsync(clock.UtcNow, cancellationToken);

        if (activeAdminCount <= 1)
        {
            throw new ApplicationRuleException("The last active administrator cannot be disabled or stripped of the Admin role.");
        }
    }

    internal static void EnsureConcurrency(ManagedUserAccount user, string concurrencyToken)
    {
        if (string.IsNullOrWhiteSpace(concurrencyToken)
            || !string.Equals(user.ConcurrencyStamp, concurrencyToken, StringComparison.Ordinal))
        {
            throw new ConcurrencyConflictException("The user was changed by another request. Reload the list and try again.");
        }
    }

    internal async Task WriteAdministrativeAuditAsync(
        string action,
        ManagedUserAccount user,
        object changes,
        CancellationToken cancellationToken)
    {
        unitOfWork.AddAudit(AuditLogEntity.Create(
                timestamp: clock.UtcNow,
                action: action,
                entityType: "ApplicationUser",
                entityId: user.Id.ToString(),
                userId: currentActor.UserId,
                userName: currentActor.UserName,
                entitySubject: user.UserName,
                changes: JsonSerializer.Serialize(changes),
                ipAddress: AuditIpAddress.Normalize(currentActor.IpAddress)));
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    internal static void EnsureSucceeded(AccountOperationResult result, string operation)
    {
        if (!result.Succeeded)
        {
            throw new ApplicationRuleException($"Failed to {operation}: {string.Join("; ", result.Errors)}");
        }
    }
}
