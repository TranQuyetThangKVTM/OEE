using System.ComponentModel.DataAnnotations;

namespace Oee.Web.Configuration;

/// <summary>Cấu hình nhịp SignalR publish và ngưỡng dữ liệu realtime bị stale.</summary>
public sealed class RealtimeUiOptions
{
    /// <summary>Tên section tương ứng trong appsettings.</summary>
    public const string SectionName = "RealtimeUi";

    /// <summary>Chu kỳ broadcast một LiveOeeModel, tính bằng giây.</summary>
    [Range(2, 60)] public int PublishSeconds { get; init; } = 2;
    /// <summary>Sample cũ hơn ngưỡng này được xem là stale, không phải tự động là Stopped.</summary>
    [Range(20, 7200)] public int StaleSeconds { get; init; } = 120;
}
