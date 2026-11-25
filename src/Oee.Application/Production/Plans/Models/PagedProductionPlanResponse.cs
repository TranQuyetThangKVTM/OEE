namespace Oee.Application.Contracts.Operations;

public sealed record PagedProductionPlanResponse(
    IReadOnlyCollection<ProductionPlanResponse> Items,
    int TotalCount);
