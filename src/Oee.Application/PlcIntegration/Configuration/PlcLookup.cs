namespace Oee.Application.Contracts.PlcIntegration;

public sealed record PlcLookup(
    Guid Id,
    string Code,
    string Name);
