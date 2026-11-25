using Oee.Application.Common;
using Oee.Domain.Entities.Oee;
using Oee.Domain.Oee;

namespace Oee.Application.Oee.Snapshots.Common;

/// <summary>Dependency và quy tắc dùng chung của các thao tác OeeSnapshot.</summary>
internal sealed class OeeSnapshotContext(
    IUseCaseExecutor requests,
    IOeeSnapshotStore store,
    IOeeAlertStore alerts,
    IApplicationUnitOfWork unitOfWork,
    IClock clock)
    : IUseCaseContext
{
    internal IUseCaseExecutor Requests => requests;
    internal IOeeSnapshotStore Store => store;
    internal IApplicationUnitOfWork UnitOfWork => unitOfWork;
    internal IClock Clock => clock;

    internal async Task EvaluateAlertsAsync(
        OeeSnapshotEntity snapshot,
        OeeCalculationResultValue result,
        CancellationToken cancellationToken)
    {
        // Ngày hiệu lực target được lấy từ period start sau khi đổi sang giờ địa phương Site.
        var scope = await store.ReadScopeAsync(snapshot.WorkUnitId, cancellationToken);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(scope.TimeZoneId);
        var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(snapshot.PeriodStart, zone).DateTime);
        var targets = await store.ReadTargetsAsync(snapshot.WorkUnitId, scope.SiteId, snapshot.PeriodType, date, cancellationToken);
        var target = OeeTargetSelector.Select(
            targets,
            snapshot.WorkUnitId,
            scope.SiteId,
            date,
            snapshot.PeriodType,
            snapshot.PeriodCode);

        if (target is null)
            return;

        // AlertEvaluator chỉ trả metric vi phạm; unique index ngăn duplicate cho cùng snapshot/metric.
        foreach (var alert in AlertEvaluator.Evaluate(result, target))
        {
            var existing = await alerts.FindUnresolvedAsync(snapshot.Id, alert.Metric, cancellationToken);

            if (existing is null)
                alerts.Add(OeeAlertEntity.Create(
                        snapshot.Id,
                        snapshot.WorkUnitId,
                        alert.Metric,
                        alert.Severity,
                        alert.Actual,
                        alert.Target,
                        clock.UtcNow,
                        target.Id));
            else
                existing.Refresh(alert.Actual, alert.Target, alert.Severity);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
