namespace Oee.Application.Contracts.Api;

/// <summary>Response một trang dữ liệu; TotalCount là tổng theo filter trước khi Skip/Take.</summary>
public sealed record PageResponse<T>(
    IReadOnlyCollection<T> Items,
    int Page,
    int PageSize,
    int TotalCount);
