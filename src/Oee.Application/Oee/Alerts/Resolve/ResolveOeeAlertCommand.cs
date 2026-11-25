using Oee.Application.Common;
using Oee.Application.Contracts.Modules;

namespace Oee.Application.Oee.Alerts.Resolve;

/// <summary>Dữ liệu yêu cầu cho thao tác ResolveOeeAlert.</summary>
public sealed record ResolveOeeAlertCommand(
    Guid Id,
    AlertMutationRequest Request) : ICommand;
