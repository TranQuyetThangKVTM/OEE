using Oee.Application.Common;

namespace Oee.Application.Downtime.Events.Reclassify;

/// <summary>Dữ liệu yêu cầu cho thao tác ReclassifyDowntimeEvent.</summary>
public sealed record ReclassifyDowntimeEventCommand(
    Guid Id,
    Guid ReasonId,
    string ConcurrencyToken)
    : ICommand;
