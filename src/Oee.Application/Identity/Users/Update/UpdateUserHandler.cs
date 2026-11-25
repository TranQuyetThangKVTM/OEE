using System.Data;
using Oee.Application.Common;
using Oee.Application.Contracts.Identity;
using Oee.Application.Identity.Users.Common;

namespace Oee.Application.Identity.Users.Update;

/// <summary>Điều phối thao tác UpdateUser trong cùng scope lưu dữ liệu.</summary>
internal sealed class UpdateUserHandler(UserManagementContext context)
    : IRequestHandler<UpdateUserCommand, UserResponse>
{
    public async Task<UserResponse> HandleAsync(
        UpdateUserCommand command,
        CancellationToken cancellationToken)
    {
        var userId = command.UserId;
        var request = command.Request;
        context.EnsureAuthorized();

        UserManagementValidator.ValidateRequest(request);
        await context.ValidateRolesAsync(request.Roles, cancellationToken);
        await using var transaction = await context.UnitOfWork.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        var user = await context.GetUserAsync(userId);
        UserManagementContext.EnsureConcurrency(user, request.ConcurrencyToken);

        if (!string.Equals(user.UserName, request.UserName.Trim(), StringComparison.Ordinal))
        {
            throw new ApplicationRuleException("Username cannot be changed from the edit-user command.");
        }

        await context.EnsureEmailIsUniqueAsync(request.Email, userId, cancellationToken);
        // Chỉ thêm/gỡ phần chênh lệch role; không xóa tất cả rồi tạo lại.
        var currentRoles = await context.Accounts.GetRolesAsync(user);
        var desiredRoles = UserManagementContext.DistinctRoles(request.Roles);

        if (context.IsSelf(userId)
            && !currentRoles.Order().SequenceEqual(desiredRoles.Order(), StringComparer.Ordinal))
        {
            throw new ApplicationRuleException("An administrator cannot change roles on their own account.");
        }

        await context.EnsureAdminRemainsActiveAsync(currentRoles, desiredRoles, cancellationToken);
        var before = new
        {
            user.UserName,
            user.DisplayName,
            user.Email,
            Roles = currentRoles.Order().ToArray()
        };
        user.DisplayName = request.DisplayName.Trim();
        user.Email = UserManagementContext.NormalizeOptional(request.Email);
        UserManagementContext.EnsureSucceeded(await context.Accounts.UpdateAsync(user), "update user");
        await context.ReplaceRolesAsync(user, currentRoles, desiredRoles);
        await context.WriteAdministrativeAuditAsync(
            "UserUpdated",
            user,
            new
            {
                Before = before,
                After = new
                {
                    user.UserName,
                    user.DisplayName,
                    user.Email,
                    Roles = desiredRoles
                }
            },
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return context.Map(user, desiredRoles);
    }
}
