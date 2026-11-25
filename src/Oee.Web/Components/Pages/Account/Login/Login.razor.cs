using Microsoft.AspNetCore.Components;

namespace Oee.Web.Components.Pages;

public partial class Login
{
    /// <summary>Route tương đối cần quay lại sau khi đăng nhập thành công.</summary>
    [SupplyParameterFromQuery(Name = "returnUrl")]
    public string? ReturnUrl { get; set; }

    /// <summary>Mã lỗi an toàn do endpoint sign-in chuyển về, không chứa chi tiết bảo mật.</summary>
    [SupplyParameterFromQuery(Name = "error")]
    public string? Error { get; set; }

    private string SafeReturnUrl => // Chỉ nhận URL tương đối bắt đầu bằng một dấu / để tránh open redirect sang website ngoài.
    !string.IsNullOrWhiteSpace(ReturnUrl) && Uri.IsWellFormedUriString(ReturnUrl, UriKind.Relative) && ReturnUrl.StartsWith('/') && !ReturnUrl.StartsWith("//", StringComparison.Ordinal) ? ReturnUrl : "/";
    private string? ErrorMessage => Error switch
    {
        // Thông báo invalid không tiết lộ username tồn tại hay mật khẩu sai, giảm khả năng dò tài khoản.
        "required" => "Vui lòng nhập tên đăng nhập và mật khẩu.",
        "locked" => "Tài khoản đang bị khóa do đăng nhập sai quá số lần cho phép.",
        "invalid" => "Tên đăng nhập hoặc mật khẩu không đúng.",
        _ => null,
    };
}
