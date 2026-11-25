namespace Oee.Application.Contracts.Operations;

public sealed record SamplePlanItemRequest(
    Guid WorkUnitId,
    Guid ProductId,
    Guid ShiftId,
    decimal PlannedQuantity,
    string? Notes);
