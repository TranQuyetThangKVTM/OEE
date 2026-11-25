using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Production.Samples.Update;

/// <summary>Dữ liệu yêu cầu cho thao tác UpdateSamplePlan.</summary>
public sealed record UpdateSamplePlanCommand(
    Guid Id,
    SamplePlanRequest Request,
    byte[] ExpectedRowVersion)
    : ICommand<SamplePlanResponse>;
