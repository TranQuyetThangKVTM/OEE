namespace Oee.Application.PlcIntegration.Monitoring;

public sealed record OpcTagDirectRead(
    string NodeId,
    string Value,
    string ActualType,
    bool IsGood,
    string StatusCode,
    DateTimeOffset? SourceAt,
    DateTimeOffset ReadAt);
