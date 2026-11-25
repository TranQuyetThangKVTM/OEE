using Microsoft.AspNetCore.Components;

namespace Oee.Web.Components.Shared;

public partial class PageFilter
{
    [Parameter]
    public RenderFragment? ChildContent { get; set; }
}
