namespace Oee.Application.Contracts.Modules;

/// <summary>Dòng Area kèm Site và số group con.</summary>
public sealed record AreaListItem(
    Guid Id,
    Guid SiteId,
    string Site,
    string Code,
    string Name,
    int WorkCenterCount,
    string Status,
    DateTimeOffset UpdatedAt,
    string RowVersion,
    string? Description = null);
