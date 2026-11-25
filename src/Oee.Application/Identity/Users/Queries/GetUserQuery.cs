using Oee.Application.Common;
using Oee.Application.Contracts.Identity;

namespace Oee.Application.Identity.Users.Queries;

/// <summary>Dữ liệu yêu cầu cho thao tác GetUser.</summary>
public sealed record GetUserQuery(Guid UserId) : IQuery<UserResponse>;
