namespace Oee.Application.Reporting;

internal sealed record EquipmentTimelineEdge(
    DateTimeOffset At,
    int Index,
    bool IsStart);
