namespace Oee.Application.Contracts.Modules;

/// <summary>Query danh sách kế hoạch mẫu với server paging.</summary>
public sealed record SamplePlanListQuery(
    string? Search = null,
    string? SortBy = null,
    bool Descending = false,
    int Page = 1,
    int PageSize = 25);
