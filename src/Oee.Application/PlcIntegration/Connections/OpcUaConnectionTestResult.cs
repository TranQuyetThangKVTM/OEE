namespace Oee.Application.Contracts.PlcIntegration;

public sealed record OpcUaConnectionTestResult(
    bool Success,
    string State,
    string Message,
    DateTimeOffset TestedAt);
