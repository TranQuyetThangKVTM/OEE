using Oee.Application.Common;
using Oee.Application.Contracts.Modules;
using Oee.Application.Oee.Alerts.Common;

namespace Oee.Application.Oee.Alerts.Queries;

/// <summary>Điều phối thao tác ListOeeTargets trong cùng scope lưu dữ liệu.</summary>
internal sealed class ListOeeTargetsHandler(AlertModuleContext context)
    : IRequestHandler<ListOeeTargetsQuery, PagedResult<TargetListItem>>
{
    public Task<PagedResult<TargetListItem>> HandleAsync(
        ListOeeTargetsQuery command,
        CancellationToken cancellationToken)
    {
        var query = command.Query;

        return context.Reads.TargetsAsync(query, cancellationToken);
    }
}
