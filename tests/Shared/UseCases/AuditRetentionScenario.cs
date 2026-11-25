using Oee.Application.Audit;
using Oee.Application.Common;
namespace Oee.Tests.UseCases;
/// <summary>Test scenario adapter: chạy handler thật trên bộ ports được truyền vào, không chứa nghiệp vụ.</summary>
internal sealed class AuditRetentionScenario : IAuditRetentionScenario
{
    private readonly ScenarioHandlerExecutor requests;
    public AuditRetentionScenario(IAuditRetentionStore store, IApplicationUnitOfWork unitOfWork, IClock clock, AuditRetentionOptions options)
    {
        requests = new ScenarioHandlerExecutor(new Dictionary<Type, object?> { [typeof(IAuditRetentionStore)] = store, [typeof(IApplicationUnitOfWork)] = unitOfWork, [typeof(IClock)] = clock, [typeof(AuditRetentionOptions)] = options });
    }

    public Task<(int DeletedCount, DateTimeOffset Cutoff)> RunAsync(CancellationToken cancellationToken) => this.requests.ExecuteAsync(new global::Oee.Application.Audit.Retention.Run.ProcessAuditRetentionCommand(), cancellationToken);
}
