namespace Oee.Application.Contracts.Identity;

/// <summary>Query server-side cho danh sách user theo search, role, state, sort và page.</summary>
public sealed record UserSearchRequest(
    string? Search = null,
    string? Role = null,
    string? State = null,
    string? SortBy = null,
    bool Descending = false,
    int Page = 1,
    int PageSize = 20);
