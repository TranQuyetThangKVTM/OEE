using Oee.Application.Common;
using Oee.Application.Contracts.Identity;

namespace Oee.Application.Identity.Users.SetRoles;

/// <summary>Dữ liệu yêu cầu cho thao tác SetUserRoles.</summary>
public sealed record SetUserRolesCommand(
    Guid UserId,
    SetUserRolesRequest Request) : ICommand<UserResponse>;
