using System.Data;
using Oee.Application.Common;
using Oee.Application.Contracts.Identity;
using Oee.Application.Identity.Users.Common;

namespace Oee.Application.Identity.Users.Create;

/// <summary>Điều phối thao tác CreateUser trong cùng scope lưu dữ liệu.</summary>
internal sealed class CreateUserHandler(UserManagementContext context)
    : IRequestHandler<CreateUserCommand, UserResponse>
{
    public async Task<UserResponse> HandleAsync(
        CreateUserCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        context.EnsureAuthorized();

        UserManagementValidator.ValidateRequest(request);
        await context.ValidateRolesAsync(request.Roles, cancellationToken);
        await using var transaction = await context.UnitOfWork.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await context.EnsureEmailIsUniqueAsync(request.Email, null, cancellationToken);
        var user = new ManagedUserAccount
        {
            Id = Guid.CreateVersion7(),
            UserName = request.UserName.Trim(),
            Email = UserManagementContext.NormalizeOptional(request.Email),
            DisplayName = request.DisplayName.Trim(),
            EmailConfirmed = true,
            LockoutEnabled = true
        };
        // Password rõ chỉ được truyền trực tiếp vào Identity API và không lưu trong biến audit.
        UserManagementContext.EnsureSucceeded(await context.Accounts.CreateAsync(user, request.Password), "create user");
        var roles = UserManagementContext.DistinctRoles(request.Roles);
        UserManagementContext.EnsureSucceeded(await context.Accounts.AddToRolesAsync(user, roles), "assign roles");
        await context.WriteAdministrativeAuditAsync(
            "UserCreated",
            user,
            new
            {
                user.UserName,
                user.DisplayName,
                user.Email,
                Roles = roles
            },
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return context.Map(user, roles);
    }
}
