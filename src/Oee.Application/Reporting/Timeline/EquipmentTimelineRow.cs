namespace Oee.Application.Contracts.Reporting;

public sealed record EquipmentTimelineRow(
    Guid WorkUnitId,
    string Code,
    string Name,
    IReadOnlyList<EquipmentTimelineSpan> Segments,
    decimal UptimeMinutes,
    decimal FailureMinutes,
    int FailureCount,
    int StopCount);
