namespace Oee.Application.PlcIntegration.Monitoring;

/// <summary>Chỉ dùng trong handler đọc trực tiếp; không trả về giao diện.</summary>
public sealed record OpcTagMonitorTarget(
    OpcTagMonitorMapping Mapping,
    OpcUaEndpointOptions Endpoint);
