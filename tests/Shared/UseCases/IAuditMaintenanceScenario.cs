using Oee.Application.Contracts.Audit;

namespace Oee.Tests.UseCases;
/// <summary>
/// Contract maintenance được tách khỏi contract đọc để giới hạn quyền retention riêng biệt.
/// </summary>
public interface IAuditMaintenanceScenario
{
    /// <summary>Tính cutoff và số dòng dự kiến bị dọn nhưng chưa thay đổi SQL.</summary>
    Task<AuditRetentionPreview> PreviewRetentionAsync(CancellationToken cancellationToken);
    /// <summary>Dọn các audit cũ hơn cutoff đã được server xác định.</summary>
    Task<AuditRetentionResult> RunRetentionAsync(AuditRetentionRequest request, CancellationToken cancellationToken);
}
