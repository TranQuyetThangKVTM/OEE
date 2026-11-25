using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Production.Plans.Queries;

/// <summary>Dữ liệu yêu cầu cho thao tác ListProductionPlans.</summary>
public sealed record ListProductionPlansQuery(
    DateTimeOffset? From,
    DateTimeOffset? Until,
    int Page,
    int PageSize)
    : IQuery<PagedProductionPlanResponse>;
