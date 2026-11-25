using Oee.Application.Common;
using Oee.Application.Contracts.Identity;

namespace Oee.Application.Identity.Users.ChangeOwnPassword;

/// <summary>Dữ liệu yêu cầu cho thao tác ChangeOwnPassword.</summary>
public sealed record ChangeOwnPasswordCommand(ChangeOwnPasswordRequest Request) : ICommand;
