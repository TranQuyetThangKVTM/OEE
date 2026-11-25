namespace Oee.Application.Contracts.Modules;

/// <summary>Dòng downtime; EndAt null là event đang mở và token dùng cho close/reclassify.</summary>
public sealed record DowntimeListItem(
    Guid Id,
    Guid WorkUnitId,
    string WorkUnit,
    Guid DowntimeReasonId,
    string ReasonCode,
    string Reason,
    DateTimeOffset StartAt,
    DateTimeOffset? EndAt,
    decimal? DurationMinutes,
    string Category,
    string Kind,
    string Source,
    string? Note,
    Guid? ProductionOutputId,
    string ConcurrencyToken);
