namespace Oee.Application.Contracts.Modules;

public sealed record MasterRow(
    Guid Id,
    string Code,
    string Name,
    string Detail,
    string Status);
