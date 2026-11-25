using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Production.Plans.PreviewBulk;

/// <summary>Dữ liệu yêu cầu cho thao tác PreviewProductionPlans.</summary>
public sealed record PreviewProductionPlansQuery(BulkProductionPlanRequest Request)
    : IQuery<BulkProductionPlanPreview>;
