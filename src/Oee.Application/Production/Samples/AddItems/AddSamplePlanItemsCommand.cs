using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Production.Samples.AddItems;

/// <summary>Dữ liệu yêu cầu cho thao tác AddSamplePlanItems.</summary>
public sealed record AddSamplePlanItemsCommand(
    Guid SamplePlanId,
    SamplePlanBulkAddItemsRequest Request)
    : ICommand<SamplePlanBulkResult>;
