using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Operations;
using Oee.Application.Security;
namespace Oee.Tests.UseCases;
/// <summary>Test scenario adapter: chạy handler thật trên bộ ports được truyền vào, không chứa nghiệp vụ.</summary>
internal sealed class ProductionPlanScenario : IProductionPlanScenario
{
    private readonly ScenarioHandlerExecutor requests;
    public ProductionPlanScenario(IProductionPlanStore store, IApplicationUnitOfWork unitOfWork, IClock clock, ICurrentActor? currentActor = null, ICapabilityChecker? capabilityChecker = null)
    {
        requests = new ScenarioHandlerExecutor(new Dictionary<Type, object?> { [typeof(IProductionPlanStore)] = store, [typeof(IApplicationUnitOfWork)] = unitOfWork, [typeof(IClock)] = clock, [typeof(ICurrentActor)] = currentActor, [typeof(ICapabilityChecker)] = capabilityChecker });
    }

    public Task<ProductionPlanResponse> CreateAsync(ProductionPlanRequest request, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Production.Plans.Create.CreateProductionPlanCommand(request), ct);
    public Task<ProductionPlanResponse> UpdateAsync(Guid id, ProductionPlanRequest request, byte[] expectedRowVersion, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Production.Plans.Update.UpdateProductionPlanCommand(id, request, expectedRowVersion), ct);
    public Task<ProductionPlanResponse> CorrectAsync(Guid id, ProductionPlanRequest request, byte[] expectedRowVersion, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Production.Plans.Correct.CorrectProductionPlanCommand(id, request, expectedRowVersion), ct);
    public Task DeleteAsync(Guid id, byte[] expectedRowVersion, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Production.Plans.Delete.DeleteProductionPlanCommand(id, expectedRowVersion), ct);
    public Task<ProductionPlanResponse> GetAsync(Guid id, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Production.Plans.Queries.GetProductionPlanQuery(id), ct);
    public Task<PagedProductionPlanResponse> ListAsync(DateTimeOffset? from, DateTimeOffset? until, int page, int pageSize, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Production.Plans.Queries.ListProductionPlansQuery(from, until, page, pageSize), ct);
    public Task<BulkProductionPlanPreview> PreviewBulkAsync(BulkProductionPlanRequest request, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Production.Plans.PreviewBulk.PreviewProductionPlansQuery(request), ct);
    public Task<BulkProductionPlanResult> CreateBulkAsync(BulkProductionPlanRequest request, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Production.Plans.Create.CreateProductionPlansCommand(request), ct);
    public Task<int> GenerateFromSampleAsync(GenerateSamplePlanRequest request, CancellationToken ct) => this.requests.ExecuteAsync(new global::Oee.Application.Production.Plans.GenerateFromSample.GenerateProductionPlansFromSampleCommand(request), ct);
}
