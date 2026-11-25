namespace Oee.Application.Security;

/// <summary>
/// Snapshot tối thiểu về người đang thực hiện request, dùng cho authorization và audit.
/// </summary>
/// <remarks>
/// Web lấy thông tin từ HttpContext/Claims và chuẩn hóa IP. Request DTO không được phép tự gửi
/// actor hoặc IP vì client có thể giả mạo các giá trị đó.
/// </remarks>
public interface ICurrentActor
{
    /// <summary>Identity Id dạng chuỗi; có thể rỗng cho tác vụ hệ thống.</summary>
    string? UserId
    {
        get;
    }

    /// <summary>Tên đăng nhập được chụp tại thời điểm thao tác.</summary>
    string UserName
    {
        get;
    }

    /// <summary>Địa chỉ IP đã được lớp Web chuẩn hóa, nếu request có HttpContext.</summary>
    string? IpAddress
    {
        get;
    }
}
