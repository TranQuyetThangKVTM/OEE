namespace Oee.Application.Contracts.Modules;

/// <summary>Dòng Enterprise kèm số Site con.</summary>
public sealed record EnterpriseListItem(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    int SiteCount,
    string Status,
    DateTimeOffset UpdatedAt,
    string RowVersion);
