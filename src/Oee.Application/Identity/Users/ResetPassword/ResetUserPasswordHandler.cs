using System.Data;
using Oee.Application.Common;
using Oee.Application.Identity.Users.Common;

namespace Oee.Application.Identity.Users.ResetPassword;

/// <summary>Điều phối thao tác ResetUserPassword trong cùng scope lưu dữ liệu.</summary>
internal sealed class ResetUserPasswordHandler(UserManagementContext context)
    : ICommandHandler<ResetUserPasswordCommand>
{
    public async Task HandleAsync(
        ResetUserPasswordCommand command,
        CancellationToken cancellationToken)
    {
        var userId = command.UserId;
        var request = command.Request;
        context.EnsureAuthorized();

        UserManagementValidator.ValidateRequest(request);
        context.EnsureNotSelf(userId, "reset the password for");
        await using var transaction = await context.UnitOfWork.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var user = await context.GetUserAsync(userId);
        UserManagementContext.EnsureConcurrency(user, request.ConcurrencyToken);
        // Reset token/mật khẩu không đi vào response, log hoặc audit JSON.
        var token = await context.Accounts.GeneratePasswordResetTokenAsync(user);
        UserManagementContext.EnsureSucceeded(
            await context.Accounts.ResetPasswordAsync(user, token, request.NewPassword),
            "reset password");
        UserManagementContext.EnsureSucceeded(
            await context.Accounts.ResetAccessFailedCountAsync(user),
            "reset failed access count");
        await context.WriteAdministrativeAuditAsync("UserPasswordReset", user, new
            {
                PasswordChanged = true
            }, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
