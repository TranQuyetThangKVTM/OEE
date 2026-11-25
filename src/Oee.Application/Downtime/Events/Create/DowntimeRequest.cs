namespace Oee.Application.Contracts.Operations;

public sealed record DowntimeRequest(
    Guid WorkUnitId,
    Guid DowntimeReasonId,
    DateTimeOffset StartAt,
    DateTimeOffset? EndAt,
    Guid? ProductionOutputId,
    string? Note);
