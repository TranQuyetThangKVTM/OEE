using Oee.Application.Contracts.Modules;

namespace Oee.Web.Realtime;

/// <summary>Kho snapshot LiveOeeModel thread-safe dùng chung giữa publisher, hub và Blazor components.</summary>
public sealed class RealtimeOeeState
{
    private readonly object sync = new();
    private LiveOeeModel? current;

    /// <summary>Event nội bộ area để component biết có model mới mà không polling database riêng.</summary>
    public event Action<LiveOeeModel>? Changed;

    /// <summary>Snapshot gần nhất; lock bảo vệ đọc/ghi giữa BackgroundService và Blazor circuits.</summary>
    public LiveOeeModel? Current
    {
        get { lock (sync) return current; }
    }

    /// <summary>Thay snapshot và gọi subscriber ngoài lock để tránh deadlock/re-entrancy.</summary>
    public void Publish(LiveOeeModel value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Action<LiveOeeModel>? handlers;
        lock (sync)
        {
            current = value;
            handlers = Changed;
        }
        handlers?.Invoke(value);
    }

    /// <summary>Giữ KPI cuối nếu có và chỉ đổi stream state thành Reconnecting.</summary>
    public LiveOeeModel MarkReconnecting(string message, DateTimeOffset at)
    {
        LiveOeeModel value;
        lock (sync)
        {
            value = current is null
                ? new LiveOeeModel([], new(0, 0, 0, 0, 0, null, null, null, null, 0, 0, 0, 0, 0),
                    null, null, null, 0, null, at, null, "Reconnecting", message)
                : current with { StreamState = "Reconnecting", StreamError = message };
        }
        Publish(value);
        return value;
    }
}
