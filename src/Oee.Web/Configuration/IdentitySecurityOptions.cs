using System.ComponentModel.DataAnnotations;

namespace Oee.Web.Configuration;

/// <summary>Password, lockout và cookie policy được bind vào ASP.NET Core Identity lúc startup.</summary>
public sealed class IdentitySecurityOptions
{
    /// <summary>Tên section tương ứng trong appsettings.</summary>
    public const string SectionName = "IdentitySecurity";

    /// <summary>Độ dài mật khẩu tối thiểu.</summary>
    [Range(8, 128)] public int RequiredLength { get; init; } = 8;
    /// <summary>Bắt buộc ít nhất một chữ hoa.</summary>
    public bool RequireUppercase { get; init; } = true;
    /// <summary>Bắt buộc ít nhất một chữ thường.</summary>
    public bool RequireLowercase { get; init; } = true;
    /// <summary>Bắt buộc ít nhất một chữ số.</summary>
    public bool RequireDigit { get; init; } = true;
    /// <summary>Bắt buộc ít nhất một ký tự không phải chữ/số.</summary>
    public bool RequireNonAlphanumeric { get; init; } = true;
    /// <summary>Số lần đăng nhập sai tối đa trước khi lockout.</summary>
    [Range(1, 20)] public int MaxFailedAccessAttempts { get; init; } = 5;
    /// <summary>Thời gian khóa tạm thời, tính bằng phút.</summary>
    [Range(1, 1440)] public int LockoutMinutes { get; init; } = 15;
    /// <summary>Thời lượng cookie đăng nhập với sliding expiration, tính bằng giờ.</summary>
    [Range(1, 24)] public int CookieHours { get; init; } = 8;
}
