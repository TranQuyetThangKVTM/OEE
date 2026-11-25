namespace Oee.Application.PlcIntegration;

/// <summary>Node tối thiểu trả về cho trình duyệt cây tag.</summary>
public sealed record OpcUaNodeDescriptor(
    string NodeId,
    string DisplayName,
    string? DataType,
    bool HasChildren,
    bool IsVariable = true);
