using Oee.Application.Common;

namespace Oee.Application.Identity.Users.Disable;

/// <summary>Dữ liệu yêu cầu cho thao tác DisableUser.</summary>
public sealed record DisableUserCommand(
    Guid UserId,
    string ConcurrencyToken) : ICommand;
