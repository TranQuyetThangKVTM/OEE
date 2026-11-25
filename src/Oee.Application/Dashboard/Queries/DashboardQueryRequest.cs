namespace Oee.Application.Contracts.Modules;

/// <summary>Bộ lọc dashboard theo ngày local và hierarchy/SKU/Shift tùy chọn.</summary>
public sealed record DashboardQueryRequest(
    DateOnly FromDate,
    DateOnly ToDate,
    Guid? SiteId = null,
    Guid? AreaId = null,
    Guid? WorkCenterId = null,
    Guid? WorkUnitId = null,
    Guid? ProductId = null,
    Guid? ShiftId = null);
