using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Operations;
using Oee.Application.Security;
namespace Oee.Tests.UseCases;
/// <summary>Test scenario adapter: chạy handler thật trên bộ ports được truyền vào, không chứa nghiệp vụ.</summary>
internal sealed class DowntimeScenario : IDowntimeScenario
{
    private readonly ScenarioHandlerExecutor requests;
    public DowntimeScenario(IDowntimeStore downtimes, IApplicationUnitOfWork unitOfWork, IClock clock, ICapabilityChecker? capabilityChecker = null)
    {
        requests = new ScenarioHandlerExecutor(new Dictionary<Type, object?> { [typeof(IDowntimeStore)] = downtimes, [typeof(IApplicationUnitOfWork)] = unitOfWork, [typeof(IClock)] = clock, [typeof(ICapabilityChecker)] = capabilityChecker });
    }

    public Task<Guid> CreateAsync(DowntimeRequest request, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Downtime.Events.Create.CreateDowntimeEventCommand(request), ct);
    public Task CloseAsync(Guid id, DateTimeOffset endAt, string concurrencyToken, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Downtime.Events.Close.CloseDowntimeEventCommand(id, endAt, concurrencyToken), ct);
    public Task ReclassifyAsync(Guid id, Guid reasonId, string concurrencyToken, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Downtime.Events.Reclassify.ReclassifyDowntimeEventCommand(id, reasonId, concurrencyToken), ct);
    public Task<int> BulkReclassifyAsync(IReadOnlyCollection<BulkReclassifyDowntimeItem> requests, Guid reasonId, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Downtime.Events.BulkReclassify.BulkReclassifyDowntimeEventCommand(requests, reasonId), ct);
}
