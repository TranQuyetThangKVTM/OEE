using Oee.Application.Audit;
using Oee.Application.Common;
using Oee.Application.Contracts.Audit;
using Oee.Application.Security;
namespace Oee.Tests.UseCases;
/// <summary>Test scenario adapter: chạy handler thật trên bộ ports được truyền vào, không chứa nghiệp vụ.</summary>
internal sealed class AuditMaintenanceScenario : IAuditMaintenanceScenario
{
    private readonly IUseCaseExecutor requests;
    public AuditMaintenanceScenario(IUseCaseExecutor requests) => this.requests = requests;
    public AuditMaintenanceScenario(IAuditRetentionStore store, IApplicationUnitOfWork unitOfWork, IClock clock, ICurrentActor currentActor, AuditRetentionOptions options, ICapabilityChecker? capabilityChecker = null)
    {
        requests = new ScenarioHandlerExecutor(new Dictionary<Type, object?> { [typeof(IAuditRetentionStore)] = store, [typeof(IApplicationUnitOfWork)] = unitOfWork, [typeof(IClock)] = clock, [typeof(ICurrentActor)] = currentActor, [typeof(AuditRetentionOptions)] = options, [typeof(ICapabilityChecker)] = capabilityChecker });
    }

    public Task<AuditRetentionPreview> PreviewRetentionAsync(CancellationToken cancellationToken) => this.requests.ExecuteAsync(new global::Oee.Application.Audit.Retention.PreviewRetention.PreviewRetentionAuditQuery(), cancellationToken);
    public Task<AuditRetentionResult> RunRetentionAsync(AuditRetentionRequest request, CancellationToken cancellationToken) => this.requests.ExecuteAsync(new global::Oee.Application.Audit.Retention.RunRetention.RunRetentionAuditCommand(request), cancellationToken);
}
