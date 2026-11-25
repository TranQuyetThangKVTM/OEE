using Microsoft.AspNetCore.Components;

namespace Oee.Web.Components.Shared;

/// <summary>Nhập ngày/giờ local bằng MudBlazor; timezone và UTC do use case gọi quyết định.</summary>
public partial class LocalDateTimeField
{
    [Parameter] public string Label { get; set; } = string.Empty;
    [Parameter] public DateTime Value { get; set; }
    [Parameter] public EventCallback<DateTime> ValueChanged { get; set; }
    [Parameter] public bool Disabled { get; set; }

    private Task SetDateAsync(DateTime? date) => date.HasValue
        ? ValueChanged.InvokeAsync(date.Value.Date + Value.TimeOfDay)
        : Task.CompletedTask;

    private Task SetTimeAsync(TimeSpan? time) => time.HasValue
        ? ValueChanged.InvokeAsync(Value.Date + time.Value)
        : Task.CompletedTask;
}
