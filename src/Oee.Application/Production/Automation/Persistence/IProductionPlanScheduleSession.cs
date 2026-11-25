namespace Oee.Application.Production.Automation;

public interface IProductionPlanScheduleSession : IAsyncDisposable
{
    ProductionPlanSchedule? Schedule
    {
        get;
    }

    Task SaveAsync(ProductionPlanSchedule schedule, CancellationToken ct);
}
