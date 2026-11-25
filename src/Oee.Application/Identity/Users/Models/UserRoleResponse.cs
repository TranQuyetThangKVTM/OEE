namespace Oee.Application.Contracts.Identity;

/// <summary>Role hợp lệ tải từ Identity store cho UI chọn; không hard-code danh sách trong Razor.</summary>
public sealed record UserRoleResponse(
    Guid Id,
    string Name);
