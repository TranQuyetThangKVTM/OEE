using Oee.Application.Contracts.Modules;

namespace Oee.Web.Components.Pages;

public partial class LiveOee
{
    private LiveOeeModel? _model;
    protected override void OnInitialized()
    {
        // Lấy snapshot gần nhất để render ngay rồi subscribe event nội bộ cho các lần SignalR publisher cập nhật.
        _model = Realtime.Current;
        Realtime.Changed += OnRealtimeChanged;
    }

    private void OnRealtimeChanged(LiveOeeModel value)
    {
        // Event đến từ BackgroundService thread; InvokeAsync chuyển về đúng renderer context của Blazor circuit.
        _ = InvokeAsync(() =>
        {
            _model = value;
            StateHasChanged();
        });
    }

    // Gỡ subscriber khi rời route để tránh memory leak và render vào component đã dispose.
    public void Dispose()
    {
        Realtime.Changed -= OnRealtimeChanged;
        GC.SuppressFinalize(this);
    }
}
