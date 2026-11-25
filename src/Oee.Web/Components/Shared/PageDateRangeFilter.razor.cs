using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Oee.Web.Components.Shared;

public partial class PageDateRangeFilter
{
    [Parameter]
    public DateTime? FromDate { get; set; }

    [Parameter]
    public EventCallback<DateTime?> FromDateChanged { get; set; }

    [Parameter]
    public DateTime? ToDate { get; set; }

    [Parameter]
    public EventCallback<DateTime?> ToDateChanged { get; set; }

    [Parameter]
    public EventCallback RangeChanged { get; set; }

    [Parameter]
    public string Label { get; set; } = "Khoảng ngày";

    [Parameter]
    public string PlaceholderStart { get; set; } = "Từ ngày";

    [Parameter]
    public string PlaceholderEnd { get; set; } = "Đến ngày";
    private DateRange CurrentRange => new(FromDate, ToDate);

    private async Task OnRangeChangedAsync(DateRange? range)
    {
        DateTime? fromDate = range?.Start;
        DateTime? toDate = range?.End;
        FromDate = fromDate;
        ToDate = toDate;
        await FromDateChanged.InvokeAsync(fromDate);
        await ToDateChanged.InvokeAsync(toDate);
        await RangeChanged.InvokeAsync();
    }
}
