using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace Oee.Web.Components.Shared;

public partial class RowAction
{
    [Parameter, EditorRequired] public string Label { get; set; } = string.Empty;
    [Parameter] public string? Icon { get; set; }
    [Parameter] public Color Color { get; set; } = Color.Default;
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public string? Href { get; set; }
    [Parameter] public EventCallback<MouseEventArgs> OnClick { get; set; }
}
