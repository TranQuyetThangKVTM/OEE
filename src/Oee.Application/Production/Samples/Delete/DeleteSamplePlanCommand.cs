using Oee.Application.Common;

namespace Oee.Application.Production.Samples.Delete;

/// <summary>Dữ liệu yêu cầu cho thao tác DeleteSamplePlan.</summary>
public sealed record DeleteSamplePlanCommand(
    Guid Id,
    byte[] ExpectedRowVersion) : ICommand;
