namespace Oee.Application.Contracts.Operations;

public sealed record SamplePlanQuantityOverride(
    Guid SamplePlanItemId,
    decimal PlannedQuantity);
