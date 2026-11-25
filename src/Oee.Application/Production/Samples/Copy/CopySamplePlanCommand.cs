using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Production.Samples.Copy;

/// <summary>Dữ liệu yêu cầu cho thao tác CopySamplePlan.</summary>
public sealed record CopySamplePlanCommand(
    Guid Id,
    string Code,
    string Name) : ICommand<SamplePlanResponse>;
