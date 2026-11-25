using Oee.Domain.Common;

namespace Oee.Application.PlcIntegration;

/// <summary>Mẫu OPC UA đã chuẩn hóa gồm source timestamp và chất lượng đọc.</summary>
public sealed record OpcUaValueSample(
    string NodeId,
    TagRoleEnum Role,
    object? Value,
    DateTimeOffset SourceTimestamp,
    bool IsGood,
    string? StatusCode);
