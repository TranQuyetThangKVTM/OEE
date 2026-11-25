namespace Oee.Application.Contracts.Modules;

/// <summary>Lookup production cho máy/group/SKU/ca/reason; Detail có thể mang timezone hoặc mô tả.</summary>
public sealed record ProductionLookupOption(
    Guid Id,
    string Code,
    string Name,
    Guid? ParentId = null,
    string? Detail = null,
    TimeOnly? StartTime = null,
    TimeOnly? EndTime = null,
    Guid? SiteId = null);
