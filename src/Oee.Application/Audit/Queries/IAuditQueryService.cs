using Oee.Application.Contracts.Audit;

namespace Oee.Application.Audit;

/// <summary>
/// Contract đọc nhật ký audit cho trang Hệ thống và API quản trị.
/// </summary>
/// <remarks>
/// Implementation ở Infrastructure thực hiện filter, sắp xếp và phân trang tại SQL.
/// Contract chỉ trả DTO nên UI không nhận EF Entity hoặc tự sửa bản ghi append-only.
/// </remarks>
public interface IAuditQueryService
{
    /// <summary>Tìm audit theo bộ lọc và trả đúng một trang dữ liệu.</summary>
    /// <param name = "request">Từ khóa, loại đối tượng, action, khoảng thời gian và thông tin trang.</param>
    /// <param name = "cancellationToken">Hủy truy vấn khi request hoặc component không còn cần kết quả.</param>
    /// <returns>Trang audit cùng tổng số dòng khớp điều kiện.</returns>
    Task<PagedAuditLogResponse> SearchAsync(
        AuditLogSearchRequest request,
        CancellationToken cancellationToken);

    /// <summary>Đọc snapshot before/after của một audit để hiển thị dialog chi tiết.</summary>
    Task<AuditLogDetailResponse> DetailAsync(Guid auditLogId, CancellationToken cancellationToken);
}
