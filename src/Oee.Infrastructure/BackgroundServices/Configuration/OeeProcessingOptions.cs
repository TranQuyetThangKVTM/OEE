using System.ComponentModel.DataAnnotations;

namespace Oee.Infrastructure.BackgroundServices.Configuration;

/// <summary>Cấu hình nhịp tính snapshot OEE và cửa sổ dữ liệu được tính lại.</summary>
/// <remarks>DataAnnotations được kiểm tra khi Web startup; giá trị sai làm host dừng sớm thay vì chạy âm thầm.</remarks>
public sealed class OeeProcessingOptions
{
    /// <summary>Tên section tương ứng trong appsettings.</summary>
    public const string SectionName = "OeeProcessing";
    /// <summary>Số giây giữa hai lần recalculation snapshot.</summary>
    [Range(5, 3600)] public int SnapshotSeconds { get; init; } = 60;
    /// <summary>Số giờ quá khứ cần tính lại để nhận các correction đến muộn.</summary>
    [Range(1, 168)] public int SnapshotLookbackHours { get; init; } = 48;
}
