using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Production.Samples.Update;

/// <summary>Dữ liệu yêu cầu cho thao tác UpdateSamplePlanItem.</summary>
public sealed record UpdateSamplePlanItemCommand(
    Guid ItemId,
    SamplePlanItemRequest Request,
    byte[] ExpectedRowVersion)
    : ICommand<SamplePlanItemResponse>;
