using Oee.Application.Common;
using Oee.Application.Contracts.Modules;

namespace Oee.Application.Oee.Alerts.Acknowledge;

/// <summary>Dữ liệu yêu cầu cho thao tác AcknowledgeOeeAlert.</summary>
public sealed record AcknowledgeOeeAlertCommand(
    Guid Id,
    AlertMutationRequest Request) : ICommand;
