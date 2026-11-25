namespace Oee.Application.Contracts.Modules;

public sealed record DashboardFilterOption(
    Guid Id,
    string Code,
    string Name,
    Guid? ParentId = null);
