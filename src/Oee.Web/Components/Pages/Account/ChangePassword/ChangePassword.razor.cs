using Microsoft.AspNetCore.Components;

namespace Oee.Web.Components.Pages;

public partial class ChangePassword
{
    /// <summary>Cho biết endpoint đã đổi mật khẩu và redirect lại thành công.</summary>
    [SupplyParameterFromQuery(Name = "success")]
    public bool Success { get; set; }

    /// <summary>Mã lỗi đã làm sạch do endpoint đổi mật khẩu trả về.</summary>
    [SupplyParameterFromQuery(Name = "error")]
    public string? Error { get; set; }
    private string? ErrorMessage // UI chỉ ánh xạ mã lỗi tổng quát, không hiển thị exception hay dữ liệu Identity nhạy cảm.
    => Error switch
    {
        "required" => "Vui lòng nhập đầy đủ mật khẩu hiện tại và mật khẩu mới.",
        "mismatch" => "Mật khẩu mới và phần xác nhận không khớp.",
        "invalid" => "Không thể đổi mật khẩu. Mật khẩu hiện tại có thể không đúng hoặc mật khẩu mới chưa đạt chính sách.",
        _ => null
    };
}
