namespace Oee.Application.Contracts.Modules;

/// <summary>Dòng WorkCenter kèm số thiết bị con.</summary>
public sealed record WorkCenterListItem(
    Guid Id,
    Guid AreaId,
    string Area,
    string Site,
    string Code,
    string Name,
    int WorkUnitCount,
    string Status,
    DateTimeOffset UpdatedAt,
    string RowVersion,
    string? Description = null);
