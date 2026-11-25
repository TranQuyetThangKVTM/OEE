namespace Oee.Application.Contracts.Modules;

/// <summary>Query target có tùy chọn chỉ lấy bản ghi hiệu lực tại ngày.</summary>
public sealed record TargetListQuery(
    string? Search = null,
    bool EffectiveOnly = false,
    DateOnly? EffectiveOn = null,
    string? SortBy = null,
    bool Descending = true,
    int Page = 1,
    int PageSize = 25);
