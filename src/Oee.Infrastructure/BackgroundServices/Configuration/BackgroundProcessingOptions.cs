using System.ComponentModel.DataAnnotations;

namespace Oee.Infrastructure.BackgroundServices.Configuration;

/// <summary>Cấu hình các tác vụ nền chạy cùng vòng đời Oee.Web.</summary>
public sealed class BackgroundProcessingOptions
{
    public const string SectionName = "BackgroundProcessing";
    public bool Enabled { get; init; } = true;
    [Range(10, 3600)] public int HeartbeatSeconds { get; init; } = 60;
}
