using Oee.Application.Contracts.Operations;

namespace Oee.Tests.UseCases;
/// <summary>Use case tạo có hiệu lực ngay, sửa phiên bản, xóa và truy vấn Production Plan.</summary>
/// <remarks>
/// Các mutation nhận expected RowVersion để chống ghi đè đồng thời. Bulk create trả preview/result
/// riêng để UI hiển thị Requested/Created/Skipped/Conflict mà không tự suy diễn kết quả.
/// </remarks>
public interface IProductionPlanScenario
{
    /// <summary>Tạo một kế hoạch trực tiếp sau validation và kiểm tra xung đột.</summary>
    Task<ProductionPlanResponse> CreateAsync(ProductionPlanRequest request, CancellationToken cancellationToken);
    /// <summary>Cập nhật kế hoạch còn được phép sửa.</summary>
    Task<ProductionPlanResponse> UpdateAsync(Guid id, ProductionPlanRequest request, byte[] expectedRowVersion, CancellationToken cancellationToken);
    /// <summary>Tạo correction/version theo rule khi plan không còn được sửa trực tiếp.</summary>
    Task<ProductionPlanResponse> CorrectAsync(Guid id, ProductionPlanRequest request, byte[] expectedRowVersion, CancellationToken cancellationToken);
    /// <summary>Xóa logic kế hoạch sau xác nhận, giữ lịch sử sản lượng và kiểm tra RowVersion.</summary>
    Task DeleteAsync(Guid id, byte[] expectedRowVersion, CancellationToken cancellationToken);
    /// <summary>Đọc chi tiết một plan dưới dạng DTO.</summary>
    Task<ProductionPlanResponse> GetAsync(Guid id, CancellationToken cancellationToken);
    /// <summary>Đọc danh sách plan theo cửa sổ UTC và paging.</summary>
    Task<PagedProductionPlanResponse> ListAsync(DateTimeOffset? from, DateTimeOffset? until, int page, int pageSize, CancellationToken cancellationToken);
    /// <summary>Sinh plan từ một mẫu theo ngày áp dụng; mẫu nguồn không bị sửa.</summary>
    Task<int> GenerateFromSampleAsync(GenerateSamplePlanRequest request, CancellationToken cancellationToken);
    /// <summary>Tính tích thiết bị × ca và các xung đột mà chưa ghi SQL.</summary>
    Task<BulkProductionPlanPreview> PreviewBulkAsync(BulkProductionPlanRequest request, CancellationToken cancellationToken);
    /// <summary>Ghi các tổ hợp hợp lệ trong một bulk request và trả kết quả typed.</summary>
    Task<BulkProductionPlanResult> CreateBulkAsync(BulkProductionPlanRequest request, CancellationToken cancellationToken);
}
