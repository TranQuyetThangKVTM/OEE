
namespace Oee.Web.Components.Layout;

public partial class MainLayout
{
    private bool _drawerOpen = true;
    private readonly WorkspaceTabState _tabs = new();
    private void ToggleDrawer()
    {
        _drawerOpen = !_drawerOpen;
    }
}
