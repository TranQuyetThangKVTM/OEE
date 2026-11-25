using Microsoft.AspNetCore.Identity;

namespace Oee.Infrastructure.Identity;

/// <summary>Identity user của OEE dùng Guid key, bổ sung tên hiển thị và cờ vô hiệu hóa.</summary>
/// <remarks>
/// PasswordHash, SecurityStamp, lockout và AccessFailedCount được kế thừa từ IdentityUser và tuyệt
/// đối không đi vào DTO. IsDisabled giữ tài khoản/lịch sử thay cho hard delete.
/// </remarks>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>Tên người dùng thân thiện hiển thị trên AppBar/audit.</summary>
    public string? DisplayName { get; set; }
    /// <summary>Cờ vô hiệu hóa nghiệp vụ, độc lập với lockout tạm thời của Identity.</summary>
    public bool IsDisabled { get; set; }
}
