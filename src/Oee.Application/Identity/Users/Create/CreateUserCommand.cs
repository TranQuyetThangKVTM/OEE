using Oee.Application.Common;
using Oee.Application.Contracts.Identity;

namespace Oee.Application.Identity.Users.Create;

/// <summary>Dữ liệu yêu cầu cho thao tác CreateUser.</summary>
public sealed record CreateUserCommand(CreateUserRequest Request) : ICommand<UserResponse>;
