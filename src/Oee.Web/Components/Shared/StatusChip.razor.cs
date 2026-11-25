using Microsoft.AspNetCore.Components;
using MudBlazor;
using Oee.Web.Components.Presentation;

namespace Oee.Web.Components.Shared;

public partial class StatusChip
{
    [Parameter, EditorRequired]
    public string Text { get; set; } = string.Empty;

    [Parameter]
    public SemanticKind Kind { get; set; } = SemanticKind.Neutral;

    [Parameter]
    public Size Size { get; set; } = Size.Small;
    private Color ColorValue => Kind switch
    {
        SemanticKind.Success => Color.Success,
        SemanticKind.Warning => Color.Warning,
        SemanticKind.Error => Color.Error,
        SemanticKind.Info => Color.Info,
        _ => Color.Default
    };
}
