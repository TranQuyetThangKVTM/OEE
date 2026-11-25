using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Production.Plans.Correct;

/// <summary>Dữ liệu yêu cầu cho thao tác CorrectProductionPlan.</summary>
public sealed record CorrectProductionPlanCommand(
    Guid Id,
    ProductionPlanRequest Request,
    byte[] ExpectedRowVersion)
    : ICommand<ProductionPlanResponse>;
