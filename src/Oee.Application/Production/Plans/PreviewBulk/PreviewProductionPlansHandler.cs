using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Production.Plans.Common;

namespace Oee.Application.Production.Plans.PreviewBulk;

/// <summary>Điều phối thao tác PreviewProductionPlans trong cùng scope lưu dữ liệu.</summary>
internal sealed class PreviewProductionPlansHandler(ProductionPlanContext context)
    : IRequestHandler<PreviewProductionPlansQuery, BulkProductionPlanPreview>
{
    public async Task<BulkProductionPlanPreview> HandleAsync(
        PreviewProductionPlansQuery command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        var candidates = await context.BuildCandidatesAsync(request, cancellationToken);
        candidates = await context.MarkExistingAsync(candidates, cancellationToken);

        return ProductionPlanContext.Preview(candidates);
    }
}
