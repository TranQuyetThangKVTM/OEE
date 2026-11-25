namespace Oee.Application.Contracts.Modules;

/// <summary>Query server-side cho danh sách kế hoạch theo scope, kỳ, sort và page.</summary>
public sealed record ProductionPlanListQuery(
    string? Search = null,
    Guid? WorkUnitId = null,
    Guid? ProductId = null,
    DateTimeOffset? From = null,
    DateTimeOffset? Until = null,
    string? SortBy = null,
    bool Descending = true,
    int Page = 1,
    int PageSize = 25);
