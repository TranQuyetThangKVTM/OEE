using System.Data;
using Oee.Application.Common;
using Oee.Application.Contracts.Identity;
using Oee.Application.Identity.Users.Common;

namespace Oee.Application.Identity.Users.Queries;

/// <summary>Điều phối thao tác SearchUsers trong cùng scope lưu dữ liệu.</summary>
internal sealed class SearchUsersHandler(UserManagementContext context)
    : IRequestHandler<SearchUsersQuery, PagedUserResponse>
{
    public async Task<PagedUserResponse> HandleAsync(
        SearchUsersQuery command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        // Read access is enforced by the authenticated Web boundary.
        var result = await context.Accounts.SearchAsync(request, context.Clock.UtcNow, cancellationToken);

        return new(
            result.Accounts.Select(user => context.Map(user, result.RolesByUser.GetValueOrDefault(user.Id) ?? [])).ToArray(),
            Math.Max(1, request.Page),
            Math.Clamp(request.PageSize, 1, 100),
            result.TotalCount);
    }
}
