using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Oee.Web.Components.Shared;

public partial class MetricCard
{
    [Parameter, EditorRequired]
    public string Label { get; set; } = string.Empty;

    [Parameter, EditorRequired]
    public string Value { get; set; } = string.Empty;

    [Parameter]
    public string Icon { get; set; } = Icons.Material.Filled.Analytics;

    [Parameter]
    public string? Description { get; set; }

    [Parameter]
    public string? Hint { get; set; }

    [Parameter]
    public Color Color { get; set; } = Color.Primary;

    private string AccentStyle => $"--metric-accent:var(--mud-palette-{(Color is Color.Default or Color.Inherit ? "info" : Color.ToString().ToLowerInvariant())})";
}
