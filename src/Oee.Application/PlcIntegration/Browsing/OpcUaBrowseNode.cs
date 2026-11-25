namespace Oee.Application.Contracts.PlcIntegration;

/// <summary>Node browse đã normalize để UI hiển thị cây mà không tham chiếu SDK OPC UA.</summary>
public sealed record OpcUaBrowseNode(
    string NodeId,
    string DisplayName,
    string? DataType,
    bool HasChildren,
    bool IsVariable = true);
