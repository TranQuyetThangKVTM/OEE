namespace Oee.Application.Contracts.Modules;

/// <summary>Payload realtime typed cho Blazor/SignalR; ratio 0..1, quantity decimal.</summary>
public sealed record LiveOeeModel(
    IReadOnlyCollection<LiveMachineItem> Machines,
    LiveFleetSummary Summary,
    string? CurrentShift,
    DateTimeOffset? ShiftStartedAt,
    DateTimeOffset? ShiftEndsAt,
    decimal ShiftProgress,
    string? TopDowntimeReason,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? SourceTimestamp,
    string StreamState = "Live",
    string? StreamError = null);
