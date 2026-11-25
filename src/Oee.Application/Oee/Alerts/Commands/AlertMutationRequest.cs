namespace Oee.Application.Contracts.Modules;

public sealed record AlertMutationRequest(
    string ConcurrencyToken,
    string? Note = null);
