namespace Oee.Infrastructure.Reporting;

/// <summary>Giới hạn an toàn cho XLSX import.</summary>
public sealed class ReportingOptions
{
    /// <summary>Tên section trong appsettings.</summary>
    public const string SectionName = "Reporting";
    /// <summary>Số dòng import tối đa mà server chấp nhận.</summary>
    public int MaxImportRows { get; init; } = 5000;
    /// <summary>Kích thước file nén tối đa, tính bằng byte.</summary>
    public long MaxImportBytes { get; init; } = 5_000_000;
}
