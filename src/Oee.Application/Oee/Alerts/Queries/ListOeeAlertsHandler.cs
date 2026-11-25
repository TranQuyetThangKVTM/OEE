using Oee.Application.Common;
using Oee.Application.Contracts.Modules;
using Oee.Application.Oee.Alerts.Common;

namespace Oee.Application.Oee.Alerts.Queries;

/// <summary>Điều phối thao tác ListOeeAlerts trong cùng scope lưu dữ liệu.</summary>
internal sealed class ListOeeAlertsHandler(AlertModuleContext context)
    : IRequestHandler<ListOeeAlertsQuery, PagedResult<AlertListItem>>
{
    public Task<PagedResult<AlertListItem>> HandleAsync(
        ListOeeAlertsQuery command,
        CancellationToken cancellationToken)
    {
        var query = command.Query;

        return context.Reads.ListAsync(query, cancellationToken);
    }
}
