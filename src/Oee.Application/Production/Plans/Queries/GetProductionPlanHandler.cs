using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Production.Plans.Common;

namespace Oee.Application.Production.Plans.Queries;

/// <summary>Điều phối thao tác GetProductionPlan trong cùng scope lưu dữ liệu.</summary>
internal sealed class GetProductionPlanHandler(ProductionPlanContext context)
    : IRequestHandler<GetProductionPlanQuery, ProductionPlanResponse>
{
    public async Task<ProductionPlanResponse> HandleAsync(
        GetProductionPlanQuery command,
        CancellationToken cancellationToken)
    {
        var id = command.Id;
        var plan = await context.Store.FindCurrentAsync(id, false, cancellationToken)
            ?? throw new KeyNotFoundException($"Production plan '{id}' was not found.");

        return ProductionPlanContext.Map(plan, await context.Store.HasOutputAsync(id, cancellationToken));
    }
}
