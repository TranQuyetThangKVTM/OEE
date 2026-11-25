using Oee.Application.Common;

namespace Oee.Application.Downtime.Events.Close;

/// <summary>Dữ liệu yêu cầu cho thao tác CloseDowntimeEvent.</summary>
public sealed record CloseDowntimeEventCommand(
    Guid Id,
    DateTimeOffset EndAt,
    string ConcurrencyToken)
    : ICommand;
