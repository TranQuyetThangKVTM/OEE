namespace Oee.Application.PlcIntegration;

/// <summary>Yêu cầu write-back một giá trị đã normalize.</summary>
public sealed record OpcUaWriteRequest(
    string NodeId,
    object? Value);
