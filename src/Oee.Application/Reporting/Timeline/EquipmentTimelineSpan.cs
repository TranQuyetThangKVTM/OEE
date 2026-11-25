namespace Oee.Application.Contracts.Reporting;

/// <summary>Observed interval or schedule used to reconstruct equipment history.</summary>
public sealed record EquipmentTimelineSpan(
    DateTimeOffset Start,
    DateTimeOffset End,
    EquipmentTimelineState State,
    Guid? EventId = null,
    string? Reason = null,
    string? Note = null,
    bool IsOpen = false);
