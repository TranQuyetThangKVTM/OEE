namespace Oee.Application.Contracts.Operations;

public sealed record GenerateSamplePlanRequest(
    Guid SamplePlanId,
    DateOnly FromDate,
    DateOnly ToDate);
