using System.ComponentModel.DataAnnotations;

namespace Oee.Web.Configuration;

/// <summary>Cấu hình nhịp refresh của Dashboard Blazor.</summary>
public sealed class DashboardUiOptions
{
    /// <summary>Tên section tương ứng trong appsettings.</summary>
    public const string SectionName = "DashboardUi";

    /// <summary>Khoảng thời gian giữa hai lần UI yêu cầu dữ liệu mới, tính bằng giây.</summary>
    [Range(10, 3600)] public int RefreshSeconds { get; init; } = 60;
}
