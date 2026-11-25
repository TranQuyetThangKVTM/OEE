namespace Oee.Application.Identity;

public sealed record AccountOperationResult(
    bool Succeeded,
    IReadOnlyCollection<string> Errors);
