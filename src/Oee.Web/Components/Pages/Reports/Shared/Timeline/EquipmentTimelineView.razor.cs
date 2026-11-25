using System.Globalization;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Oee.Application.Contracts.Reporting;
using Oee.Application.Reporting;

namespace Oee.Web.Components.Pages.Reports;

public partial class EquipmentTimelineView
{
    [Parameter, EditorRequired]
    public EquipmentTimelineData Data { get; set; } = default!;

    [Parameter]
    public string Title { get; set; } = "Dòng thời gian trạng thái thiết bị";

    private static readonly EquipmentTimelineState[] Legend = [EquipmentTimelineState.Running, EquipmentTimelineState.Failure, EquipmentTimelineState.Stopped, EquipmentTimelineState.PlannedStop, EquipmentTimelineState.OtherStop, EquipmentTimelineState.Unknown, EquipmentTimelineState.NoProduction, EquipmentTimelineState.Future];
    private decimal StopMinutes => Data.Rows.SelectMany(x => x.Segments).Where(x => EquipmentTimelineBuilder.IsStop(x.State)).Sum(x => (decimal)(x.End - x.Start).TotalMinutes);

    private string AxisLabel(DateTimeOffset at) => TimeZoneInfo.ConvertTime(at, TimeZoneInfo.FindSystemTimeZoneById(Data.TimeZoneId)).ToString((Data.End - Data.Start).TotalHours <= 30 ? "HH:mm" : "dd/MM", CultureInfo.CurrentCulture);
    private string Position(EquipmentTimelineSpan span) => "left:" + ((span.Start - Data.Start).TotalMilliseconds / (Data.End - Data.Start).TotalMilliseconds * 100).ToString("0.########", CultureInfo.InvariantCulture) + "%;width:" + ((span.End - span.Start).TotalMilliseconds / (Data.End - Data.Start).TotalMilliseconds * 100).ToString("0.########", CultureInfo.InvariantCulture) + "%";
    private string Tooltip(EquipmentTimelineSpan span) => EquipmentTimeText.State(span.State) + " · " + EquipmentTimeText.Local(span.Start, Data.TimeZoneId) + " → " + EquipmentTimeText.Local(span.End, Data.TimeZoneId) + " · " + EquipmentTimeText.Duration((decimal)(span.End - span.Start).TotalMinutes) + (string.IsNullOrWhiteSpace(span.Reason) ? "" : " · " + span.Reason);
    private async Task ShowAsync(EquipmentTimelineRow row, EquipmentTimelineSpan span)
    {
        var parameters = new DialogParameters<EquipmentTimelineDetailDialog>
        {
            {
                x => x.Row,
                row
            },
            {
                x => x.Segment,
                span
            },
            {
                x => x.TimeZoneId,
                Data.TimeZoneId
            }
        };
        await DialogService.ShowAsync<EquipmentTimelineDetailDialog>("Chi tiết thời gian thiết bị", parameters, new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseButton = true });
    }
}
