using Oee.Application.Common;
using Oee.Application.Contracts.Identity;
using Oee.Application.Identity.Users.Common;

namespace Oee.Application.Identity.Users.Queries;

/// <summary>Điều phối thao tác GetUser trong cùng scope lưu dữ liệu.</summary>
internal sealed class GetUserHandler(UserManagementContext context)
    : IRequestHandler<GetUserQuery, UserResponse>
{
    public async Task<UserResponse> HandleAsync(
        GetUserQuery command,
        CancellationToken cancellationToken)
    {
        var userId = command.UserId;
        // Read access is enforced by the authenticated Web boundary.
        cancellationToken.ThrowIfCancellationRequested();
        var user = await context.GetUserAsync(userId);

        return context.Map(user, await context.Accounts.GetRolesAsync(user));
    }
}
