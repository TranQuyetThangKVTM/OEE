using Oee.Application.Contracts.Operations;

namespace Oee.Tests.UseCases;
/// <summary>Use case quản lý kế hoạch mẫu cha và các dòng thiết bị × ca × SKU.</summary>
/// <remarks>
/// Bulk create/add phải chạy nhất quán: không để cha không có dòng và chống trùng cả Application
/// lẫn unique index SQL. Production Plan sinh từ mẫu giữ khóa truy vết về mẫu và dòng nguồn.
/// </remarks>
public interface ISampleProductionPlanScenario
{
    Task<SamplePlanResponse> CreateAsync(SamplePlanRequest request, CancellationToken cancellationToken);
    Task<SamplePlanResponse> UpdateAsync(Guid id, SamplePlanRequest request, byte[] expectedRowVersion, CancellationToken cancellationToken);
    Task<SamplePlanResponse> CopyAsync(Guid id, string code, string name, CancellationToken cancellationToken);
    Task DeleteAsync(Guid id, byte[] expectedRowVersion, CancellationToken cancellationToken);
    Task<SamplePlanItemResponse> UpdateItemAsync(Guid itemId, SamplePlanItemRequest request, byte[] expectedRowVersion, CancellationToken cancellationToken);
    Task DeleteItemsAsync(IReadOnlyCollection<SamplePlanItemConcurrency> items, CancellationToken cancellationToken);
    /// <summary>Tạo mẫu cha và toàn bộ dòng trong cùng use case/transaction.</summary>
    Task<SamplePlanBulkResult> CreateWithItemsAsync(SamplePlanBulkCreateRequest request, CancellationToken cancellationToken);
    /// <summary>Preview số dòng mới/trùng khi thêm tích thiết bị × ca vào mẫu.</summary>
    Task<SamplePlanBulkPreview> PreviewAddItemsAsync(Guid samplePlanId, SamplePlanBulkAddItemsRequest request, CancellationToken cancellationToken);
    /// <summary>Thêm các dòng hợp lệ theo bulk, không SaveChanges riêng từng vòng lặp.</summary>
    Task<SamplePlanBulkResult> AddItemsAsync(Guid samplePlanId, SamplePlanBulkAddItemsRequest request, CancellationToken cancellationToken);
}
