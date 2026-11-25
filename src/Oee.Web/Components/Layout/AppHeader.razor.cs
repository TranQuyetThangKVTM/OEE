using Microsoft.AspNetCore.Components;

namespace Oee.Web.Components.Layout;

public partial class AppHeader
{
    /// <summary>Callback do MainLayout truyền vào để mở/đóng responsive drawer.</summary>
    [Parameter]
    public EventCallback DrawerToggleRequested { get; set; }
}
