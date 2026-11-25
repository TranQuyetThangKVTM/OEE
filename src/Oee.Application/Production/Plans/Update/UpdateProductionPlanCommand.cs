using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Production.Plans.Update;

/// <summary>Dữ liệu yêu cầu cho thao tác UpdateProductionPlan.</summary>
public sealed record UpdateProductionPlanCommand(
    Guid Id,
    ProductionPlanRequest Request,
    byte[] ExpectedRowVersion)
    : ICommand<ProductionPlanResponse>;
