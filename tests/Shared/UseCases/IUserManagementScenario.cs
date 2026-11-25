using Oee.Application.Contracts.Identity;

namespace Oee.Tests.UseCases;
/// <summary>
/// Các use case quản trị tài khoản ASP.NET Core Identity được trang Hệ thống và API gọi tới.
/// </summary>
/// <remarks>
/// Application điều phối gateway Identity ở Infrastructure, không để PasswordHash hoặc
/// SecurityStamp đi qua contract. Mỗi mutation phải kiểm tra quyền server-side, quy tắc Admin cuối
/// cùng và concurrency trước khi lưu Identity tables và audit.
/// </remarks>
public interface IUserManagementScenario
{
    /// <summary>Tìm user theo từ khóa, filter và phân trang tại server.</summary>
    Task<PagedUserResponse> SearchAsync(UserSearchRequest request, CancellationToken cancellationToken);
    /// <summary>Đọc chi tiết user theo Id để mở dialog cập nhật.</summary>
    Task<UserResponse> GetAsync(Guid userId, CancellationToken cancellationToken);
    /// <summary>Đọc danh sách Role hợp lệ từ hệ thống, không hard-code trong Razor.</summary>
    Task<IReadOnlyCollection<UserRoleResponse>> RolesAsync(CancellationToken cancellationToken);
    /// <summary>Tạo user, hash mật khẩu bằng Identity và gán Role nhất quán.</summary>
    Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken);
    /// <summary>Cập nhật các trường hồ sơ được phép với concurrency token.</summary>
    Task<UserResponse> UpdateAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken);
    /// <summary>Áp dụng chênh lệch Role sau khi kiểm tra Role hợp lệ và Admin cuối cùng.</summary>
    Task<UserResponse> SetRolesAsync(Guid userId, SetUserRolesRequest request, CancellationToken cancellationToken);
    /// <summary>Đặt mật khẩu mới qua API Identity; mật khẩu rõ không được audit hoặc log.</summary>
    Task ResetPasswordAsync(Guid userId, ResetUserPasswordRequest request, CancellationToken cancellationToken);
    /// <summary>Vô hiệu hóa user an toàn thay cho hard delete.</summary>
    Task DisableAsync(Guid userId, string concurrencyToken, CancellationToken cancellationToken);
    /// <summary>Cho actor đã đăng nhập đổi mật khẩu của chính mình.</summary>
    Task ChangeOwnPasswordAsync(ChangeOwnPasswordRequest request, CancellationToken cancellationToken);
}
