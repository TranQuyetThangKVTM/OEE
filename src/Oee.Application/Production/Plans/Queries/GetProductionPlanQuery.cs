using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Production.Plans.Queries;

/// <summary>Dữ liệu yêu cầu cho thao tác GetProductionPlan.</summary>
public sealed record GetProductionPlanQuery(Guid Id) : IQuery<ProductionPlanResponse>;
