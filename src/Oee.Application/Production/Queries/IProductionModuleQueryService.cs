using Oee.Application.Contracts.Modules;

namespace Oee.Application.Modules;

/// <summary>
/// Tập query chỉ đọc phục vụ các trang Production; mọi danh sách lớn phải filter/paging tại SQL.
/// </summary>
public interface IProductionModuleQueryService
{
    /// <summary>Đọc lookup active dùng trong các form Production.</summary>
    Task<ProductionLookups> LookupsAsync(CancellationToken cancellationToken);

    /// <summary>Đọc một trang kế hoạch sản xuất.</summary>
    Task<PagedResult<ProductionPlanListItem>> PlansAsync(
        ProductionPlanListQuery query,
        CancellationToken cancellationToken);

    /// <summary>Đọc một trang sự kiện downtime.</summary>
    Task<PagedResult<DowntimeListItem>> DowntimeAsync(
        DowntimeListQuery query,
        CancellationToken cancellationToken);

    /// <summary>Đọc chi tiết downtime theo Id.</summary>
    Task<DowntimeListItem> DowntimeAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Đọc một trang kế hoạch mẫu.</summary>
    Task<PagedResult<SamplePlanListItem>> SamplesAsync(
        SamplePlanListQuery query,
        CancellationToken cancellationToken);

    /// <summary>Đọc phần cha và thống kê của một kế hoạch mẫu.</summary>
    Task<SamplePlanDetail> SampleAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Đọc các dòng con của kế hoạch mẫu với sorting/paging tại server.</summary>
    Task<PagedResult<SamplePlanItemDetail>> SampleItemsAsync(
        Guid samplePlanId,
        string? sortBy,
        bool descending,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}
