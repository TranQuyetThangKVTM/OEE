using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Production.Samples.Create;

/// <summary>Dữ liệu yêu cầu cho thao tác CreateSamplePlan.</summary>
public sealed record CreateSamplePlanCommand(SamplePlanRequest Request) : ICommand<SamplePlanResponse>;
