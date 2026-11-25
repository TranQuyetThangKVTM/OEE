using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Production.Plans.Create;

/// <summary>Dữ liệu yêu cầu cho thao tác CreateProductionPlans.</summary>
public sealed record CreateProductionPlansCommand(BulkProductionPlanRequest Request)
    : ICommand<BulkProductionPlanResult>;
