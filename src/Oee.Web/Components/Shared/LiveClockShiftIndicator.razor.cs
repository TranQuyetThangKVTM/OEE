using Microsoft.AspNetCore.Components;

namespace Oee.Web.Components.Shared;

public partial class LiveClockShiftIndicator
{
    // TimeZoneInfo.ConvertTime đổi UTC sang timezone cấu hình; timer/CTS được giữ để dispose cùng component.
    [Parameter]
    public TimeZoneInfo TimeZone { get; set; } = TimeZoneInfo.Local;

    private DateTimeOffset now;
    private PeriodicTimer? timer;
    private CancellationTokenSource? stop;
    // Ca đêm gồm 22:00–24:00 và 00:00–06:00; start=-2 biểu diễn 22:00 của ngày trước khi tính progress.
    private string ShiftName
    {
        get
        {
            var hour = now.Hour;
            return hour >= 6 && hour < 14 ? "Ca sáng · S1" : hour >= 14 && hour < 22 ? "Ca chiều · S2" : "Ca đêm · S3";
        }
    }

    private double Progress
    {
        get
        {
            var start = now.Hour >= 6 && now.Hour < 14 ? 6 : now.Hour >= 14 && now.Hour < 22 ? 14 : now.Hour >= 22 ? 22 : -2;
            var elapsed = (now.TimeOfDay - TimeSpan.FromHours(start)).TotalMinutes;
            return Math.Clamp(elapsed / 480d * 100d, 0, 100);
        }
    }

    // OnInitialized tạo vòng tick nền; InvokeAsync chuyển cập nhật UI về đúng Blazor renderer context.
    protected override void OnInitialized()
    {
        now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZone);
        stop = new();
        timer = new(TimeSpan.FromSeconds(1));
        _ = TickAsync(stop.Token);
    }

    private async Task TickAsync(CancellationToken ct)
    {
        try
        {
            while (timer is not null && await timer.WaitForNextTickAsync(ct))
            {
                now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZone);
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (OperationCanceledException)when (ct.IsCancellationRequested)
        {
        }
    }

    // Hủy và dispose tài nguyên để timer không giữ component/circuit sau khi điều hướng.
    public ValueTask DisposeAsync()
    {
        stop?.Cancel();
        timer?.Dispose();
        stop?.Dispose();
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
