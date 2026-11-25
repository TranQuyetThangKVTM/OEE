using Oee.Application.Contracts.Modules;

namespace Oee.Application.Contracts.Operations;

/// <summary>Một dòng được chọn rõ ràng để xóa mềm; không tự chọn/cascade các cấp con.</summary>
public sealed record EquipmentHierarchyDeleteItem(
    EquipmentHierarchyKind Kind,
    Guid Id,
    string RowVersion);
