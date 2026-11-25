using Oee.Application.Common;
using Oee.Application.Contracts.Identity;
using Oee.Application.Identity.Users.Common;

namespace Oee.Application.Identity.Users.Queries;

/// <summary>Điều phối thao tác ListUserRoles trong cùng scope lưu dữ liệu.</summary>
internal sealed class ListUserRolesHandler(UserManagementContext context)
    : IRequestHandler<ListUserRolesQuery, IReadOnlyCollection<UserRoleResponse>>
{
    public Task<IReadOnlyCollection<UserRoleResponse>> HandleAsync(
        ListUserRolesQuery command,
        CancellationToken cancellationToken)
    {
        // Read access is enforced by the authenticated Web boundary.
        return context.Accounts.ReadRolesAsync(cancellationToken);
    }
}
