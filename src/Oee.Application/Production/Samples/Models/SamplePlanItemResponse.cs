namespace Oee.Application.Contracts.Operations;

public sealed record SamplePlanItemResponse(
    Guid Id,
    Guid SamplePlanId,
    Guid WorkUnitId,
    Guid ProductId,
    Guid ShiftId,
    decimal PlannedQuantity,
    string? Notes,
    string RowVersion);
