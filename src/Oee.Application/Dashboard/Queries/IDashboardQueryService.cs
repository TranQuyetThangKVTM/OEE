using Oee.Application.Contracts.Modules;

namespace Oee.Application.Modules;

/// <summary>Đọc bộ lọc và mô hình Dashboard tổng quan từ dữ liệu sản xuất/OEE thật.</summary>
public interface IDashboardQueryService
{
    /// <summary>Trả các Site, Area, WorkUnit, Product và Shift dùng trong bộ lọc.</summary>
    Task<DashboardFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken);

    /// <summary>Truy vấn KPI, trend, Pareto và trạng thái fleet theo filter.</summary>
    Task<DashboardModel> GetAsync(DashboardQueryRequest request, CancellationToken cancellationToken);

    /// <summary>OEE ngày và lũy kế tháng của Line, lấy Filler trong phạm vi tổ chức làm máy đại diện.</summary>
    Task<LineOeeSummary> GetLineOeeAsync(DashboardQueryRequest request, CancellationToken cancellationToken);
}
