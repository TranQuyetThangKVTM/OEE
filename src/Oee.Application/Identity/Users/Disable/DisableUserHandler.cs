using System.Data;
using Oee.Application.Common;
using Oee.Application.Identity.Users.Common;
using Oee.Application.Security;

namespace Oee.Application.Identity.Users.Disable;

/// <summary>Điều phối thao tác DisableUser trong cùng scope lưu dữ liệu.</summary>
internal sealed class DisableUserHandler(UserManagementContext context) : ICommandHandler<DisableUserCommand>
{
    public async Task HandleAsync(DisableUserCommand command, CancellationToken cancellationToken)
    {
        var userId = command.UserId;
        var concurrencyToken = command.ConcurrencyToken;
        context.EnsureAuthorized();

        context.EnsureNotSelf(userId, "disable");
        await using var transaction = await context.UnitOfWork.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var user = await context.GetUserAsync(userId);
        UserManagementContext.EnsureConcurrency(user, concurrencyToken);

        if (await context.Accounts.IsInRoleAsync(user, RoleCapabilityMatrix.Admin))
        {
            await context.EnsureNotLastActiveAdminAsync(cancellationToken);
        }

        user.IsDisabled = true;
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        UserManagementContext.EnsureSucceeded(await context.Accounts.UpdateAsync(user), "disable user");
        await context.WriteAdministrativeAuditAsync("UserDisabled", user, new
            {
                IsDisabled = true
            }, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
