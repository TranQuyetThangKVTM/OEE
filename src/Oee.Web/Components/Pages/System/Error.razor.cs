using System.Diagnostics;
using Microsoft.AspNetCore.Components;

namespace Oee.Web.Components.Pages;

public partial class Error
{
    [CascadingParameter]
    private HttpContext? HttpContext { get; set; }
    private string? RequestId { get; set; }
    private bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    protected override void OnInitialized()
    {
        // Ưu tiên Activity trace hiện tại; fallback TraceIdentifier của HttpContext.
        RequestId = Activity.Current?.Id ?? HttpContext?.TraceIdentifier;
    }
}
