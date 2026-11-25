using System.ComponentModel.DataAnnotations;
using Oee.Application.PlcIntegration;

namespace Oee.Infrastructure.BackgroundServices.Configuration;

/// <summary>Cấu hình ingestion, reconnect, backpressure và write-back OPC UA.</summary>
public sealed class OpcUaWorkerOptions : IOpcUaWriteBackSettings
{
    /// <summary>Tên section tương ứng trong appsettings.</summary>
    public const string SectionName = "OpcUaWorker";
    /// <summary>Chọn Fake cho Development/UAT hoặc Sdk cho OPC UA server thật.</summary>
    [RegularExpression("^(Fake|Sdk)$", ErrorMessage = "Adapter must be Fake or Sdk.")]
    public string Adapter { get; init; } = "Sdk";
    /// <summary>Bật riêng chiều ghi KPI về PLC; mặc định false để vận hành read-only an toàn.</summary>
    public bool WriteBackEnabled { get; init; }
    /// <summary>Sức chứa bounded Channel; khi đầy producer phải chờ để tạo backpressure.</summary>
    [Range(10, 100000)] public int ChannelCapacity { get; init; } = 2000;
    /// <summary>Thời gian chờ reconnect ban đầu, trước khi exponential backoff tăng dần.</summary>
    [Range(1, 300)] public int InitialReconnectSeconds { get; init; } = 2;
    /// <summary>Giới hạn trên của reconnect backoff.</summary>
    [Range(5, 3600)] public int MaximumReconnectSeconds { get; init; } = 120;
    /// <summary>Quá thời gian này không có sample mới thì connectivity chuyển NoData.</summary>
    [Range(5, 3600)] public int NoDataSeconds { get; init; } = 30;
    /// <summary>Đọc lại tag định kỳ để kiểm tra dữ liệu ngay cả khi server không phát notification do giá trị không đổi.</summary>
    [Range(1, 300)] public int ReadRefreshSeconds { get; init; } = 10;
    /// <summary>Quá hạn đọc trực tiếp thì đóng session và reconnect; không chờ vô hạn.</summary>
    [Range(1, 300)] public int ReadTimeoutSeconds { get; init; } = 15;
    /// <summary>Chu kỳ write-back KPI về output tag.</summary>
    [Range(5, 3600)] public int WriteBackSeconds { get; init; } = 60;
    /// <summary>Chu kỳ đọc lại cấu hình mapping để phát hiện activate/update/delete.</summary>
    [Range(5, 300)] public int ConfigurationRefreshSeconds { get; init; } = 10;
}
