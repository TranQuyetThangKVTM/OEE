using Microsoft.AspNetCore.Components;

namespace Oee.Web.Components.Shared;

public partial class PageHeader
{
    [CascadingParameter]
    public Oee.Web.Components.Layout.WorkspaceTabState? WorkspaceTabs { get; set; }

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    protected override void OnParametersSet()
        => WorkspaceTabs?.SetTitle(Title, "/" + Navigation.ToBaseRelativePath(Navigation.Uri));

    [Parameter]
    public string Title { get; set; } = string.Empty;

    [Parameter]
    public string Subtitle { get; set; } = string.Empty;

    [Parameter]
    public RenderFragment? Actions { get; set; }

    [Parameter]
    public RenderFragment? Metrics { get; set; }

    [Parameter]
    public EventCallback OnRefresh { get; set; }

    [Parameter]
    public bool RefreshDisabled { get; set; }
}
