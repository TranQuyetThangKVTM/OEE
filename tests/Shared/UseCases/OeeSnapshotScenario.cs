using Oee.Application.Common;
using Oee.Application.Oee;
using Oee.Domain.Common;
namespace Oee.Tests.UseCases;
/// <summary>Test scenario adapter: chạy handler thật trên bộ ports được truyền vào, không chứa nghiệp vụ.</summary>
internal sealed class OeeSnapshotScenario : IOeeSnapshotScenario
{
    private readonly ScenarioHandlerExecutor requests;
    public OeeSnapshotScenario(IOeeSnapshotStore store, IOeeAlertStore alerts, IApplicationUnitOfWork unitOfWork, IClock clock)
    {
        requests = new ScenarioHandlerExecutor(new Dictionary<Type, object?> { [typeof(IOeeSnapshotStore)] = store, [typeof(IOeeAlertStore)] = alerts, [typeof(IApplicationUnitOfWork)] = unitOfWork, [typeof(IClock)] = clock });
    }

    public Task<Guid> RecalculateAsync(Guid workUnitId, OeePeriodTypeEnum periodType, string periodCode, DateTimeOffset periodStart, DateTimeOffset periodEnd, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Oee.Snapshots.Recalculate.RecalculateOeeSnapshotCommand(workUnitId, periodType, periodCode, periodStart, periodEnd), ct);
    public Task<int> RecalculateLookbackAsync(TimeSpan lookback, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Oee.Snapshots.RecalculateLookback.RecalculateRecentOeeSnapshotsCommand(lookback), ct);
}
