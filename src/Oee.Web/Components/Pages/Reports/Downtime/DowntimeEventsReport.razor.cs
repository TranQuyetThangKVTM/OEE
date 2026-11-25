using Microsoft.AspNetCore.Components;

namespace Oee.Web.Components.Pages.Reports;

public partial class DowntimeEventsReport
{
    /// <summary>Lý do dừng được chọn từ Pareto hoặc màn hình trước.</summary>
    [SupplyParameterFromQuery]
    public Guid? ReasonId { get; set; }

    /// <summary>Từ khóa drill-down tùy chọn cần áp dụng khi workspace khởi tạo.</summary>
    [SupplyParameterFromQuery]
    public string? Search { get; set; }
}
