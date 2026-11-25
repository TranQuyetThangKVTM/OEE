namespace Oee.Application.Contracts.Modules;

/// <summary>Query server-side cho danh sách cảnh báo SLO.</summary>
public sealed record AlertListQuery(
    string? Search = null,
    string? State = null,
    string? Severity = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    string? SortBy = null,
    bool Descending = true,
    int Page = 1,
    int PageSize = 25);
