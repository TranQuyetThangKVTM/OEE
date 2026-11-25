namespace Oee.Application.PlcIntegration.Monitoring;

/// <summary>Cache bộ nhớ theo tiến trình; không ghi database và không tham gia tính sản lượng.</summary>
public interface IOpcTagMonitorBuffer
{
    void Record(
        Guid deviceId,
        OpcUaValueSample sample,
        DateTimeOffset receivedAt,
        bool fromReadRefresh);

    void Configure(Guid deviceId, IReadOnlyCollection<OpcUaReadRequest> reads);

    void SetConnection(Guid deviceId, string state);

    void RemoveDevice(Guid deviceId);

    OpcTagMonitorSnapshot Snapshot(DateTimeOffset now);
}
