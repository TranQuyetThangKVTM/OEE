using Oee.Application.Common;
using Oee.Application.Contracts.Modules;

namespace Oee.Application.Oee.Alerts.Queries;

/// <summary>Dữ liệu yêu cầu cho thao tác ListOeeTargets.</summary>
public sealed record ListOeeTargetsQuery(TargetListQuery Query) : IQuery<PagedResult<TargetListItem>>;
