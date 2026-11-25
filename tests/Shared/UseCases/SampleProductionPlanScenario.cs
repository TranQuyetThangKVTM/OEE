using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Operations;
using Oee.Application.Security;
namespace Oee.Tests.UseCases;
/// <summary>Test scenario adapter: chạy handler thật trên bộ ports được truyền vào, không chứa nghiệp vụ.</summary>
internal sealed class SampleProductionPlanScenario : ISampleProductionPlanScenario
{
    private readonly ScenarioHandlerExecutor requests;
    public SampleProductionPlanScenario(ISampleProductionPlanStore store, IApplicationUnitOfWork unitOfWork, IClock clock, ICurrentActor? currentActor = null)
    {
        requests = new ScenarioHandlerExecutor(new Dictionary<Type, object?> { [typeof(ISampleProductionPlanStore)] = store, [typeof(IApplicationUnitOfWork)] = unitOfWork, [typeof(IClock)] = clock, [typeof(ICurrentActor)] = currentActor });
    }

    public Task<SamplePlanResponse> CreateAsync(SamplePlanRequest request, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Production.Samples.Create.CreateSamplePlanCommand(request), ct);
    public Task<SamplePlanResponse> UpdateAsync(Guid id, SamplePlanRequest request, byte[] expectedRowVersion, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Production.Samples.Update.UpdateSamplePlanCommand(id, request, expectedRowVersion), ct);
    public Task<SamplePlanResponse> CopyAsync(Guid id, string code, string name, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Production.Samples.Copy.CopySamplePlanCommand(id, code, name), ct);
    public Task DeleteAsync(Guid id, byte[] expectedRowVersion, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Production.Samples.Delete.DeleteSamplePlanCommand(id, expectedRowVersion), ct);
    public Task<SamplePlanItemResponse> UpdateItemAsync(Guid itemId, SamplePlanItemRequest request, byte[] expectedRowVersion, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Production.Samples.Update.UpdateSamplePlanItemCommand(itemId, request, expectedRowVersion), ct);
    public Task DeleteItemsAsync(IReadOnlyCollection<SamplePlanItemConcurrency> requests, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Production.Samples.Delete.DeleteSamplePlanItemsCommand(requests), ct);
    public Task<SamplePlanBulkResult> CreateWithItemsAsync(SamplePlanBulkCreateRequest request, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Production.Samples.Create.CreateSamplePlanWithItemsCommand(request), ct);
    public Task<SamplePlanBulkResult> AddItemsAsync(Guid samplePlanId, SamplePlanBulkAddItemsRequest request, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Production.Samples.AddItems.AddSamplePlanItemsCommand(samplePlanId, request), ct);
    public Task<SamplePlanBulkPreview> PreviewAddItemsAsync(Guid samplePlanId, SamplePlanBulkAddItemsRequest request, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Production.Samples.PreviewAddItems.PreviewSamplePlanItemsQuery(samplePlanId, request), ct);
}
