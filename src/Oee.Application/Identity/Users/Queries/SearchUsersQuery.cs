using Oee.Application.Common;
using Oee.Application.Contracts.Identity;

namespace Oee.Application.Identity.Users.Queries;

/// <summary>Dữ liệu yêu cầu cho thao tác SearchUsers.</summary>
public sealed record SearchUsersQuery(UserSearchRequest Request) : IQuery<PagedUserResponse>;
