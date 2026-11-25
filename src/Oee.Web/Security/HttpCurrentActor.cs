using System.Security.Claims;
using Oee.Application.Security;

namespace Oee.Web.Security;

/// <summary>Cung cấp identity và IP của request hiện tại cho audit/Application mà không phụ thuộc HttpContext trực tiếp.</summary>
public sealed class HttpCurrentActor(IHttpContextAccessor accessor, ScopedPrincipalAccessor? session = null) : ICurrentActor
{
    /// <summary>ClaimsPrincipal do ASP.NET Core cookie authentication xây dựng.</summary>
    private ClaimsPrincipal? Principal => session?.User ?? accessor.HttpContext?.User;
    /// <summary>Identity user Id; null cho request không đăng nhập/system.</summary>
    public string? UserId => Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
    /// <summary>Tác vụ nền không có HttpContext được ghi nhận là System; request chưa đăng nhập là Anonymous.</summary>
    public string UserName => Principal?.Identity?.Name ?? (accessor.HttpContext is null ? "System" : "Anonymous");
    /// <summary>Remote IP do server connection xác định; client không được gửi IP qua DTO.</summary>
    public string? IpAddress => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
