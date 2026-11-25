using System.Data;
using Oee.Application.Common;
using Oee.Application.Identity.Users.Common;

namespace Oee.Application.Identity.Users.ChangeOwnPassword;

/// <summary>Điều phối thao tác ChangeOwnPassword trong cùng scope lưu dữ liệu.</summary>
internal sealed class ChangeOwnPasswordHandler(UserManagementContext context)
    : ICommandHandler<ChangeOwnPasswordCommand>
{
    public async Task HandleAsync(
        ChangeOwnPasswordCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        UserManagementValidator.ValidateRequest(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!Guid.TryParse(context.CurrentActor.UserId, out var actorId))
        {
            throw new ApplicationRuleException("An authenticated user is required to change a password.");
        }

        await using var transaction = await context.UnitOfWork.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var user = await context.GetUserAsync(actorId);
        UserManagementContext.EnsureSucceeded(
            await context.Accounts.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword),
            "change password");
        await context.WriteAdministrativeAuditAsync(
            "OwnPasswordChanged",
            user,
            new
            {
                PasswordChanged = true
            },
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
