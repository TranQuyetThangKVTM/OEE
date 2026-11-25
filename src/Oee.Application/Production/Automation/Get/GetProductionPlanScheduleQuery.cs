using Oee.Application.Common;

namespace Oee.Application.Production.Automation;

public sealed record GetProductionPlanScheduleQuery : IRequest<ProductionPlanSchedule?>;
