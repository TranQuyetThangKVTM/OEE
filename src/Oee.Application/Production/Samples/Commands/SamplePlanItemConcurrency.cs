namespace Oee.Application.Contracts.Operations;

public sealed record SamplePlanItemConcurrency(
    Guid Id,
    string RowVersion);
