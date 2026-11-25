using Oee.Application.Common;
using Oee.Application.Contracts.Modules;

namespace Oee.Application.Oee.Alerts.EndTarget;

/// <summary>Dữ liệu yêu cầu cho thao tác EndOeeTarget.</summary>
public sealed record EndOeeTargetCommand(
    Guid Id,
    EndTargetRequest Request) : ICommand;
