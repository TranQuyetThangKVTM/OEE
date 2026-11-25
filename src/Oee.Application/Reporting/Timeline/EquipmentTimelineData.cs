namespace Oee.Application.Contracts.Reporting;

public sealed record EquipmentTimelineData(
    DateTimeOffset Start,
    DateTimeOffset End,
    DateTimeOffset ObservedUntil,
    string TimeZoneId,
    IReadOnlyList<EquipmentTimelineRow> Rows);
