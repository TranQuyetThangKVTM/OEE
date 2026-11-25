using Oee.Application.Common;

namespace Oee.Application.Oee.Alerts.Reevaluate;

/// <summary>Dữ liệu yêu cầu cho thao tác ReevaluateOeeAlert.</summary>
public sealed record ReevaluateOeeAlertCommand(
    Guid Id,
    string ConcurrencyToken) : ICommand;
