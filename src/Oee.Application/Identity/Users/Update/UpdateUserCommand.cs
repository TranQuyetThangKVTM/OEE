using Oee.Application.Common;
using Oee.Application.Contracts.Identity;

namespace Oee.Application.Identity.Users.Update;

/// <summary>Dữ liệu yêu cầu cho thao tác UpdateUser.</summary>
public sealed record UpdateUserCommand(
    Guid UserId,
    UpdateUserRequest Request) : ICommand<UserResponse>;
