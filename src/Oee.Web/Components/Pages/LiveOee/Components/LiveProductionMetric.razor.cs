using Microsoft.AspNetCore.Components;

namespace Oee.Web.Components.Pages;

public partial class LiveProductionMetric
{
    [Parameter, EditorRequired]
    public string Label { get; set; } = string.Empty;

    [Parameter]
    public decimal? Value { get; set; }

    [Parameter, EditorRequired]
    public string Icon { get; set; } = string.Empty;
}
