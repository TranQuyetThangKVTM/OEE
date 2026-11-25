namespace Oee.Application.Contracts.PlcIntegration;

/// <summary>Tag mapping động bằng role/direction/NodeId/data type, không dùng OPC SDK type.</summary>
public sealed record PlcTagConfiguration(
    Guid? Id,
    string Role,
    string Direction,
    string NodeId,
    string DataType,
    bool IsRequired);
