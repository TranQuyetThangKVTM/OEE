using Microsoft.AspNetCore.Components;

namespace Oee.Web.Components.Shared;

public partial class RowActionMenu
{
    [Parameter] public string Label { get; set; } = "Thao tác bản ghi";
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }
}
