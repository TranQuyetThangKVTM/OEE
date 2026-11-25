using Microsoft.AspNetCore.Components;

namespace Oee.Web.Components.Pages.Reports;

public partial class DeviceOeeDetail
{
    /// <summary>Thiết bị được truyền từ KPI/biểu đồ của trang trước để mở đúng drill-down.</summary>
    [SupplyParameterFromQuery]
    public Guid? WorkUnitId { get; set; }
}
