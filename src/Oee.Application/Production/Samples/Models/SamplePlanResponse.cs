namespace Oee.Application.Contracts.Operations;

public sealed record SamplePlanResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string RowVersion);
