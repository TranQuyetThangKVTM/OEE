namespace Oee.Application.PlcIntegration;

/// <summary>Một frame telemetry đầy đủ đã gom từ các tag của cùng device mapping.</summary>
/// <remarks>
/// SourceTimestamp là thời điểm mới nhất của frame phục vụ trạng thái/idempotency.
/// TotalCounterTimestamp giữ riêng thời điểm counter tổng đổi để gán sản lượng đúng ca;
/// frame cũ chưa có trường này dùng SourceTimestamp để tương thích replay.
/// </remarks>
public sealed record DeviceTelemetryFrame(
    Guid DeviceMappingId,
    bool IsRunning,
    decimal TotalCounter,
    decimal DefectCounter,
    string? FaultCode,
    string? ProductCode,
    DateTimeOffset SourceTimestamp,
    string SampleKey,
    bool IsReconnect = false,
    DateTimeOffset? ReceivedAt = null,
    bool IsReadRefresh = false,
    DateTimeOffset? TotalCounterTimestamp = null,
    DateTimeOffset? RunningTimestamp = null);
