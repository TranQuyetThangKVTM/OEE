namespace Oee.Application.Contracts.Modules;

/// <summary>Kết quả paging tối thiểu dùng nội bộ giữa Application và UI.</summary>
public sealed record PagedResult<T>(
    IReadOnlyCollection<T> Items,
    int TotalCount);
