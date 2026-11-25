namespace Oee.Application.Contracts.Modules;

/// <summary>Query thống nhất cho bảng master: filter, scope cha/liên quan, effective, sort và server paging.</summary>
public sealed record MasterListQuery(
    string? Search = null,
    Guid? ParentId = null,
    Guid? RelatedId = null,
    string? Category = null,
    string? Status = null,
    bool EffectiveOnly = false,
    string? SortBy = null,
    bool Descending = false,
    int Page = 1,
    int PageSize = 25);
