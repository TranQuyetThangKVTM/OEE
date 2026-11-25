using Oee.Application.Security;

namespace Oee.Infrastructure.Security;

/// <summary>Actor mặc định cho tác vụ nền không có HttpContext/user đăng nhập.</summary>
/// <remarks>Web thay thế bằng actor đọc Claims; Worker dùng “System” và không giả lập IP.</remarks>
public sealed class SystemCurrentActor : ICurrentActor
{
    /// <summary>Tác vụ hệ thống không có Identity user Id.</summary>
    public string? UserId => null;
    /// <summary>Tên snapshot ghi vào audit của background area.</summary>
    public string UserName => "System";
    /// <summary>Không có client IP ngoài request HTTP.</summary>
    public string? IpAddress => null;
}
