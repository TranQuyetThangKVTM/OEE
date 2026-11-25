using Oee.Application.Common;
using Oee.Application.Contracts.Modules;

namespace Oee.Application.Oee.Alerts.Queries;

/// <summary>Dữ liệu yêu cầu cho thao tác ListOeeAlerts.</summary>
public sealed record ListOeeAlertsQuery(AlertListQuery Query) : IQuery<PagedResult<AlertListItem>>;
