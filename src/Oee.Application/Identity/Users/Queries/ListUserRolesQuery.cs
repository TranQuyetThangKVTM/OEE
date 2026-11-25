using Oee.Application.Common;
using Oee.Application.Contracts.Identity;

namespace Oee.Application.Identity.Users.Queries;

/// <summary>Dữ liệu yêu cầu cho thao tác ListUserRoles.</summary>
public sealed record ListUserRolesQuery() : IQuery<IReadOnlyCollection<UserRoleResponse>>;
