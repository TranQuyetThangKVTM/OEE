using Oee.Application.Common;
using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.Operations;
using Oee.Application.Oee;
using Oee.Application.Security;
namespace Oee.Tests.UseCases;
/// <summary>Test scenario adapter: chạy handler thật trên bộ ports được truyền vào, không chứa nghiệp vụ.</summary>
internal sealed class AlertModuleScenario : IAlertModuleScenario
{
    private readonly ScenarioHandlerExecutor requests;
    public AlertModuleScenario(IAlertReadStore reads, IOeeAlertStore alerts, IOeeTargetStore targets, IApplicationUnitOfWork unitOfWork, IOeeSnapshotScenario snapshots, IEffectiveConfigurationScenario effectiveConfigurations, IClock clock, ICurrentActor currentActor)
    {
        requests = new ScenarioHandlerExecutor(new Dictionary<Type, object?> { [typeof(IAlertReadStore)] = reads, [typeof(IOeeAlertStore)] = alerts, [typeof(IOeeTargetStore)] = targets, [typeof(IApplicationUnitOfWork)] = unitOfWork, [typeof(IOeeSnapshotScenario)] = snapshots, [typeof(IEffectiveConfigurationScenario)] = effectiveConfigurations, [typeof(IClock)] = clock, [typeof(ICurrentActor)] = currentActor });
    }

    public Task<PagedResult<AlertListItem>> ListAsync(AlertListQuery query, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Oee.Alerts.Queries.ListOeeAlertsQuery(query), ct);
    public Task<PagedResult<TargetListItem>> TargetsAsync(TargetListQuery query, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Oee.Alerts.Queries.ListOeeTargetsQuery(query), ct);
    public Task AcknowledgeAsync(Guid id, AlertMutationRequest request, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Oee.Alerts.Acknowledge.AcknowledgeOeeAlertCommand(id, request), ct);
    public Task ResolveAsync(Guid id, AlertMutationRequest request, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Oee.Alerts.Resolve.ResolveOeeAlertCommand(id, request), ct);
    public Task ReevaluateAsync(Guid id, string concurrencyToken, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Oee.Alerts.Reevaluate.ReevaluateOeeAlertCommand(id, concurrencyToken), ct);
    public Task<Guid> CreateTargetVersionAsync(EffectiveTargetRequest request, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Oee.Targets.AddTargetVersion.AddTargetVersionCommand(request), ct);
    public Task EndTargetAsync(Guid id, EndTargetRequest request, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Oee.Alerts.EndTarget.EndOeeTargetCommand(id, request), ct);
    public Task DeleteTargetAsync(Guid id, string rowVersion, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Oee.Alerts.Delete.DeleteOeeTargetCommand(id, rowVersion), ct);
}
