using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.Operations;

namespace Oee.Tests.UseCases;
/// <summary>Use case đọc và thay đổi vòng đời cảnh báo/target OEE.</summary>
public interface IAlertModuleScenario
{
    /// <summary>Đọc cảnh báo theo filter và phân trang.</summary>
    Task<PagedResult<AlertListItem>> ListAsync(AlertListQuery query, CancellationToken cancellationToken);
    /// <summary>Đọc các phiên bản target theo filter và phân trang.</summary>
    Task<PagedResult<TargetListItem>> TargetsAsync(TargetListQuery query, CancellationToken cancellationToken);
    /// <summary>Ghi nhận người vận hành đã tiếp nhận cảnh báo.</summary>
    Task AcknowledgeAsync(Guid id, AlertMutationRequest request, CancellationToken cancellationToken);
    /// <summary>Đánh dấu cảnh báo đã được xử lý.</summary>
    Task ResolveAsync(Guid id, AlertMutationRequest request, CancellationToken cancellationToken);
    /// <summary>Đánh giá lại cảnh báo bằng snapshot/target hiện hành.</summary>
    Task ReevaluateAsync(Guid id, string concurrencyToken, CancellationToken cancellationToken);
    /// <summary>Tạo phiên bản target mới, giữ lại lịch sử target cũ.</summary>
    Task<Guid> CreateTargetVersionAsync(EffectiveTargetRequest request, CancellationToken cancellationToken);
    /// <summary>Kết thúc hiệu lực một target tại mốc được yêu cầu.</summary>
    Task EndTargetAsync(Guid id, EndTargetRequest request, CancellationToken cancellationToken);
    /// <summary>Xóa mềm target đủ điều kiện với kiểm tra RowVersion.</summary>
    Task DeleteTargetAsync(Guid id, string rowVersion, CancellationToken cancellationToken);
}
