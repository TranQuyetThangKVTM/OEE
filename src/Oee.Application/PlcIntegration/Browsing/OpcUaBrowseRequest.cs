namespace Oee.Application.Contracts.PlcIntegration;

public sealed record OpcUaBrowseRequest(
    Guid ConnectionId,
    string? ParentNodeId);
