namespace Oee.Application.Contracts.Modules;

/// <summary>Dòng cảnh báo; Actual/Target/Difference là ratio 0..1 và token chống ghi đè.</summary>
public sealed record AlertListItem(
    Guid Id,
    Guid SnapshotId,
    string WorkUnit,
    string WorkUnitName,
    string Period,
    string PeriodType,
    string Metric,
    string Severity,
    string State,
    decimal Actual,
    decimal Target,
    decimal Difference,
    DateTimeOffset TriggeredAt,
    DateTimeOffset? AcknowledgedAt,
    string? AcknowledgedBy,
    DateTimeOffset? ResolvedAt,
    string? Note,
    string ConcurrencyToken);
