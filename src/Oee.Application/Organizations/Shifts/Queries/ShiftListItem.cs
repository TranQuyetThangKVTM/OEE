namespace Oee.Application.Contracts.Modules;

/// <summary>Dòng Shift; CrossesMidnight cho UI giải thích trường hợp 22:00–06:00.</summary>
public sealed record ShiftListItem(
    Guid Id,
    Guid SiteId,
    string Site,
    string Code,
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool CrossesMidnight,
    string Status,
    DateTimeOffset UpdatedAt,
    string RowVersion);
