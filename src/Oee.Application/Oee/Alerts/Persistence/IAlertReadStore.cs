using Oee.Application.Contracts.Modules;

namespace Oee.Application.Oee;

public interface IAlertReadStore
{
    Task<PagedResult<AlertListItem>> ListAsync(AlertListQuery query, CancellationToken ct);

    Task<PagedResult<TargetListItem>> TargetsAsync(TargetListQuery query, CancellationToken ct);
}
