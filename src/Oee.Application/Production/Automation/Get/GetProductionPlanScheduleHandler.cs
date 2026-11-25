using Oee.Application.Common;

namespace Oee.Application.Production.Automation;

internal sealed class GetProductionPlanScheduleHandler(PlanAutomationContext context)
    : IRequestHandler<GetProductionPlanScheduleQuery, ProductionPlanSchedule?>
{
    public async Task<ProductionPlanSchedule?> HandleAsync(
        GetProductionPlanScheduleQuery request,
        CancellationToken cancellationToken)
    {
        context.EnsureManage();

        if (!context.Schedules.Exists)
            return null;
        await using var session = await context.Schedules.OpenAsync(cancellationToken);

        return session.Schedule;
    }
}
