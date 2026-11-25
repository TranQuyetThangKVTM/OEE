using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Production.Samples.PreviewAddItems;

/// <summary>Dữ liệu yêu cầu cho thao tác PreviewSamplePlanItems.</summary>
public sealed record PreviewSamplePlanItemsQuery(
    Guid SamplePlanId,
    SamplePlanBulkAddItemsRequest Request)
    : IQuery<SamplePlanBulkPreview>;
