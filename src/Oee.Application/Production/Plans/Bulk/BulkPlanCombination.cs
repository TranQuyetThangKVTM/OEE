namespace Oee.Application.Contracts.Operations;

/// <summary>Một tổ hợp preview cùng state Valid/Skipped/Conflict và lý do cụ thể.</summary>
public sealed record BulkPlanCombination(
    Guid WorkUnitId,
    string WorkUnit,
    Guid ShiftId,
    string Shift,
    Guid ProductId,
    string Product,
    decimal PlannedQuantity,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    Guid? SourceSamplePlanItemId,
    string State,
    string? Reason);
