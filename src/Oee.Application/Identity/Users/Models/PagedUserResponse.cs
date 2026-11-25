namespace Oee.Application.Contracts.Identity;

/// <summary>Một trang user và tổng số bản ghi phù hợp filter.</summary>
public sealed record PagedUserResponse(
    IReadOnlyCollection<UserResponse> Items,
    int Page,
    int PageSize,
    int TotalCount);
