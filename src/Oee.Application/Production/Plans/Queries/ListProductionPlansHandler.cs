using System.Data;
using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Production.Plans.Common;

namespace Oee.Application.Production.Plans.Queries;

/// <summary>Điều phối thao tác ListProductionPlans trong cùng scope lưu dữ liệu.</summary>
internal sealed class ListProductionPlansHandler(ProductionPlanContext context)
    : IRequestHandler<ListProductionPlansQuery, PagedProductionPlanResponse>
{
    public async Task<PagedProductionPlanResponse> HandleAsync(
        ListProductionPlansQuery command,
        CancellationToken cancellationToken)
    {
        var from = command.From;
        var until = command.Until;
        var page = command.Page;
        var pageSize = command.PageSize;
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var (items, count) = await context.Store.ReadCurrentPageAsync(from, until, page, pageSize, cancellationToken);
        // Một query phụ theo tập Id tránh gọi Any riêng cho từng plan (N+1).
        var ids = items.Select(x => x.Id).ToArray();
        var withOutput = await context.Store.ReadPlanIdsWithOutputAsync(ids, cancellationToken);

        return new(items.Select(x => ProductionPlanContext.Map(x, withOutput.Contains(x.Id))).ToArray(), count);
    }
}
