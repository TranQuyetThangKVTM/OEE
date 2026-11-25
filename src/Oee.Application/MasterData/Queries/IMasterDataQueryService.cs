using Oee.Application.Contracts.Modules;

namespace Oee.Application.Modules;

/// <summary>Query chi tiết, lookup và danh sách phân trang cho Master Data/Organization.</summary>
public interface IMasterDataQueryService
{
    /// <summary>Đọc toàn bộ lookup active cần thiết cho các dialog master.</summary>
    Task<MasterDataLookups> LookupsAsync(CancellationToken cancellationToken);

    /// <summary>Đọc Product theo Id để cập nhật bằng DTO tách tracking.</summary>
    Task<ProductListItem> ProductAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Đọc WorkUnitStandard theo Id.</summary>
    Task<WorkUnitStandardListItem> StandardAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Đọc Shift theo Id.</summary>
    Task<ShiftListItem> ShiftAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Đọc DowntimeReason theo Id.</summary>
    Task<DowntimeReasonListItem> DowntimeReasonAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Đọc WorkUnit theo Id.</summary>
    Task<WorkUnitListItem> WorkUnitAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Đọc WorkCenter theo Id.</summary>
    Task<WorkCenterListItem> WorkCenterAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Đọc Area theo Id.</summary>
    Task<AreaListItem> AreaAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Đọc Enterprise theo Id.</summary>
    Task<EnterpriseListItem> EnterpriseAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Đọc Site theo Id.</summary>
    Task<SiteListItem> SiteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Tìm và phân trang Product tại server.</summary>
    Task<PagedResult<ProductListItem>> ProductsAsync(
        MasterListQuery query,
        CancellationToken cancellationToken);

    /// <summary>Tìm và phân trang WorkUnitStandard tại server.</summary>
    Task<PagedResult<WorkUnitStandardListItem>> StandardsAsync(
        MasterListQuery query,
        CancellationToken cancellationToken);

    /// <summary>Tìm và phân trang Shift tại server.</summary>
    Task<PagedResult<ShiftListItem>> ShiftsAsync(
        MasterListQuery query,
        CancellationToken cancellationToken);

    /// <summary>Tìm và phân trang DowntimeReason tại server.</summary>
    Task<PagedResult<DowntimeReasonListItem>> DowntimeReasonsAsync(
        MasterListQuery query,
        CancellationToken cancellationToken);
}
