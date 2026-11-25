using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Production.Plans.Create;

/// <summary>Dữ liệu yêu cầu cho thao tác CreateProductionPlan.</summary>
public sealed record CreateProductionPlanCommand(ProductionPlanRequest Request)
    : ICommand<ProductionPlanResponse>;
