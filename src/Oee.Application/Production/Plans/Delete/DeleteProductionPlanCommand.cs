using Oee.Application.Common;

namespace Oee.Application.Production.Plans.Delete;

/// <summary>Dữ liệu yêu cầu cho thao tác DeleteProductionPlan.</summary>
public sealed record DeleteProductionPlanCommand(
    Guid Id,
    byte[] ExpectedRowVersion) : ICommand;
