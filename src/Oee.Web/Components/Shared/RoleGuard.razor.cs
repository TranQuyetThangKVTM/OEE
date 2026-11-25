using Microsoft.AspNetCore.Components;

namespace Oee.Web.Components.Shared;

public partial class RoleGuard
{
    [Parameter, EditorRequired]
    public string Policy { get; set; } = string.Empty;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public RenderFragment? Unauthorized { get; set; }
}
