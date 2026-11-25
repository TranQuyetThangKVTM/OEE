using Oee.Application.Common;
using Oee.Application.Contracts.Identity;

namespace Oee.Application.Identity.Users.ResetPassword;

/// <summary>Dữ liệu yêu cầu cho thao tác ResetUserPassword.</summary>
public sealed record ResetUserPasswordCommand(
    Guid UserId,
    ResetUserPasswordRequest Request) : ICommand;
