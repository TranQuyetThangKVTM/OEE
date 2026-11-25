using System.ComponentModel.DataAnnotations;

namespace Oee.Application.Audit;

/// <summary>
/// Các tùy chọn điều khiển việc lưu giữ và dọn nhật ký audit cũ.
/// </summary>
/// <remarks>
/// Lớp Application chỉ mô tả cấu hình hợp lệ; việc đọc cấu hình và xóa dữ liệu SQL do
/// composition root cùng Infrastructure đảm nhiệm. Các thuộc tính <see cref = "RangeAttribute"/>
/// được kiểm tra khi ứng dụng khởi động để tránh một chu kỳ hoặc thời hạn không hợp lệ.
/// </remarks>
public sealed class AuditRetentionOptions
{
    /// <summary>Tên section tương ứng trong appsettings.</summary>
    public const string SectionName = "AuditRetention";
    /// <summary>Cho biết cơ chế dọn log theo lịch có được bật hay không.</summary>
    public bool Enabled
    {
        get; init;
    }

    /// <summary>Số tháng giữ audit; mặc định là 18 tháng.</summary>
    [Range(1, 120)]
    public int Months { get; init; } = 18;

    /// <summary>Khoảng cách giữa hai lần chạy maintenance, tính theo giờ.</summary>
    [Range(1, 24)]
    public int RunEveryHours { get; init; } = 24;
}
