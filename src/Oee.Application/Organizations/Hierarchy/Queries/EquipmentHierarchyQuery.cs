namespace Oee.Application.Contracts.Modules;

/// <summary>Đọc một trang root hoặc con trực tiếp; tìm kiếm giữ các tổ tiên dẫn tới kết quả.</summary>
public sealed record EquipmentHierarchyQuery(
    EquipmentHierarchyKind? ParentKind = null,
    Guid? ParentId = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 25,
    string? EnterpriseSearch = null,
    string? SiteSearch = null,
    string? AreaSearch = null,
    string? WorkCenterSearch = null,
    string? WorkUnitSearch = null);
