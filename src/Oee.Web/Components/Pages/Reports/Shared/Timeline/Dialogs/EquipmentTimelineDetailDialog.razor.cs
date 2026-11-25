using Microsoft.AspNetCore.Components;
using MudBlazor;
using Oee.Application.Contracts.Reporting;

namespace Oee.Web.Components.Pages.Reports;

public partial class EquipmentTimelineDetailDialog
{
    [CascadingParameter]
    public IMudDialogInstance Dialog { get; set; } = default!;

    [Parameter, EditorRequired]
    public EquipmentTimelineRow Row { get; set; } = default!;

    [Parameter, EditorRequired]
    public EquipmentTimelineSpan Segment { get; set; } = default!;

    [Parameter, EditorRequired]
    public string TimeZoneId { get; set; } = string.Empty;

    private void Close() => Dialog.Close();
}
