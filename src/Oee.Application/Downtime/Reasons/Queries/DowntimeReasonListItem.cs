namespace Oee.Application.Contracts.Modules;

/// <summary>Dòng lý do downtime đã projection category/kind/status thành chuỗi cho UI.</summary>
public sealed record DowntimeReasonListItem(
    Guid Id,
    string Code,
    string Name,
    string Category,
    string Kind,
    string? Description,
    string Status,
    bool IsSystemReason,
    DateTimeOffset UpdatedAt,
    string RowVersion);
