namespace Oee.Application.Contracts.Operations;

public sealed record SamplePlanRequest(
    string Code,
    string Name,
    string? Description);
