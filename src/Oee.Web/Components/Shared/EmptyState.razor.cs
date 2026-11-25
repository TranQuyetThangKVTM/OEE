using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Oee.Web.Components.Shared;

public partial class EmptyState
{
    [Parameter]
    public string Icon { get; set; } = Icons.Material.Outlined.Inbox;

    [Parameter]
    public string Title { get; set; } = "Không có dữ liệu";

    [Parameter]
    public string Message { get; set; } = "Hãy thay đổi bộ lọc hoặc thử lại sau.";

    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
