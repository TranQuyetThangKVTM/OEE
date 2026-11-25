namespace Oee.Application.Contracts.Operations;

/// <summary>Response plan không expose navigation Entity; RowVersion bảo vệ update cạnh tranh.</summary>
public sealed record ProductionPlanResponse(
    Guid Id,
    Guid VersionSeriesId,
    int Version,
    Guid WorkUnitId,
    Guid ProductId,
    Guid? ShiftId,
    string Code,
    DateTimeOffset PlannedStart,
    DateTimeOffset PlannedEnd,
    decimal PlannedQuantity,
    string? Notes,
    string RowVersion,
    bool HasOutput,
    Guid? SourceSampleProductionPlanId = null,
    Guid? SourceSampleProductionPlanItemId = null);
