using Microsoft.AspNetCore.Components;

namespace Oee.Web.Components.Shared;

public partial class LoadingOverlay
{
    [Parameter]
    public bool Visible { get; set; }

    [Parameter]
    public string Message { get; set; } = "Đang tải dữ liệu";
}
