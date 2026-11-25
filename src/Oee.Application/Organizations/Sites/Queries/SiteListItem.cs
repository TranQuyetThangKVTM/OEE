namespace Oee.Application.Contracts.Modules;

/// <summary>Dòng Site gồm timezone và số Area/Shift để UI cảnh báo phụ thuộc.</summary>
public sealed record SiteListItem(
    Guid Id,
    Guid EnterpriseId,
    string Enterprise,
    string Code,
    string Name,
    string TimeZoneId,
    int AreaCount,
    int ShiftCount,
    string Status,
    DateTimeOffset UpdatedAt,
    string RowVersion,
    string? Description = null);
