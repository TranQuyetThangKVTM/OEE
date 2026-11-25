using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Production.Plans.GenerateFromSample;

/// <summary>Dữ liệu yêu cầu cho thao tác GenerateProductionPlansFromSample.</summary>
public sealed record GenerateProductionPlansFromSampleCommand(GenerateSamplePlanRequest Request)
    : ICommand<int>;
