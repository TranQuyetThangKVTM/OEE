namespace Oee.Application.Contracts.Modules;

/// <summary>Node phẳng cho bảng cây; ChildCount là số con trực tiếp chưa bị xóa mềm.</summary>
public sealed record EquipmentHierarchyNode(
    Guid Id,
    Guid? ParentId,
    EquipmentHierarchyKind Kind,
    string Code,
    string Name,
    int ChildCount,
    string Status,
    DateTimeOffset UpdatedAt,
    string RowVersion,
    string? OperationalState = null,
    string? ConnectivityState = null,
    string? Description = null);
