using Oee.Application.Contracts.Modules;

namespace Oee.Application.Modules;

/// <summary>Đọc cây theo từng nhánh, không tải toàn bộ WorkUnit vào browser.</summary>
public interface IEquipmentHierarchyQueryService
{
    /// <summary>Phân trang tại SQL; parent null đọc Enterprise, có giá trị đọc cấp con kế tiếp.</summary>
    Task<PagedResult<EquipmentHierarchyNode>> GetAsync(
        EquipmentHierarchyQuery query,
        CancellationToken cancellationToken);
}
