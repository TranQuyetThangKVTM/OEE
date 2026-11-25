namespace Oee.Application.Contracts.Modules;

/// <summary>Query downtime theo máy/reason/category/open/time range và paging.</summary>
public sealed record DowntimeListQuery(
    string? Search = null,
    Guid? WorkUnitId = null,
    Guid? DowntimeReasonId = null,
    bool UnclassifiedOnly = false,
    bool OpenOnly = false,
    DateTimeOffset? From = null,
    DateTimeOffset? Until = null,
    string? SortBy = null,
    bool Descending = true,
    int Page = 1,
    int PageSize = 25);
