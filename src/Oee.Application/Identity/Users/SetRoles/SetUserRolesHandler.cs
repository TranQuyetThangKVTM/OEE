using System.Data;
using Oee.Application.Common;
using Oee.Application.Contracts.Identity;
using Oee.Application.Identity.Users.Common;

namespace Oee.Application.Identity.Users.SetRoles;

/// <summary>Điều phối thao tác SetUserRoles trong cùng scope lưu dữ liệu.</summary>
internal sealed class SetUserRolesHandler(UserManagementContext context)
    : IRequestHandler<SetUserRolesCommand, UserResponse>
{
    public async Task<UserResponse> HandleAsync(
        SetUserRolesCommand command,
        CancellationToken cancellationToken)
    {
        var userId = command.UserId;
        var request = command.Request;
        context.EnsureAuthorized();

        UserManagementValidator.ValidateRequest(request);
        await context.ValidateRolesAsync(request.Roles, cancellationToken);
        context.EnsureNotSelf(userId, "change roles on");
        await using var transaction = await context.UnitOfWork.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var user = await context.GetUserAsync(userId);
        UserManagementContext.EnsureConcurrency(user, request.ConcurrencyToken);
        var currentRoles = await context.Accounts.GetRolesAsync(user);
        var desiredRoles = UserManagementContext.DistinctRoles(request.Roles);
        await context.EnsureAdminRemainsActiveAsync(currentRoles, desiredRoles, cancellationToken);
        await context.ReplaceRolesAsync(user, currentRoles, desiredRoles);
        await context.WriteAdministrativeAuditAsync(
            "UserRolesChanged",
            user,
            new
            {
                Before = currentRoles.Order().ToArray(),
                After = desiredRoles
            },
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return context.Map(user, desiredRoles);
    }
}
