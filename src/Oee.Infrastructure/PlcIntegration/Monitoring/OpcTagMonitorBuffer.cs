using Microsoft.Extensions.Options;
using Oee.Application.PlcIntegration;
using Oee.Application.PlcIntegration.Monitoring;
using Oee.Domain.Common;
using Oee.Infrastructure.BackgroundServices.Configuration;

namespace Oee.Infrastructure.PlcIntegration;

/// <summary>Chỉ giữ giá trị cuối của các input đang cấu hình. Snapshot bất biến, an toàn giữa worker và nhiều màn hình.</summary>
public sealed class OpcTagMonitorBuffer(IOptions<OpcUaWorkerOptions> options) : IOpcTagMonitorBuffer
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, HashSet<(string NodeId, TagRoleEnum Role)>> _configured = [];
    private readonly Dictionary<(Guid DeviceId, string NodeId, TagRoleEnum Role), OpcTagObservation> _values = [];
    private readonly Dictionary<Guid, string> _connections = [];

    public void Configure(Guid deviceId, IReadOnlyCollection<OpcUaReadRequest> reads)
    {
        lock (_gate)
        {
            var keys = reads.Select(read => (read.NodeId, read.Role)).ToHashSet();
            _configured[deviceId] = keys;
            foreach (var key in _values.Keys.Where(key => key.DeviceId == deviceId && !keys.Contains((key.NodeId, key.Role))).ToArray())
                _values.Remove(key);
        }
    }

    public void Record(Guid deviceId, OpcUaValueSample sample, DateTimeOffset receivedAt, bool fromReadRefresh)
    {
        var value = OpcTagValueFormatter.Format(sample.Value);
        var type = sample.Value?.GetType().Name ?? "—";
        lock (_gate)
        {
            // Callback đến muộn sau khi xóa/đổi ánh xạ không làm sống lại tag cũ.
            if (!_configured.TryGetValue(deviceId, out var keys) || !keys.Contains((sample.NodeId, sample.Role))) return;
            var key = (deviceId, sample.NodeId, sample.Role);
            _values.TryGetValue(key, out var previous);
            if (previous is not null && receivedAt < previous.ReceivedAt) return;
            var changedAt = previous is null ? null : previous.Value != value || previous.ActualType != type ? receivedAt : previous.ChangedAt;
            _values[key] = new(deviceId, sample.NodeId, sample.Role, value, type, sample.IsGood && sample.Value is not null,
                sample.Value is null ? (sample.StatusCode ?? "Unknown") + " · Giá trị rỗng" : sample.StatusCode ?? (sample.IsGood ? "Good" : "Bad"),
                sample.SourceTimestamp, receivedAt, changedAt, fromReadRefresh);
        }
    }

    public void SetConnection(Guid deviceId, string state)
    {
        lock (_gate) _connections[deviceId] = state;
    }

    public void RemoveDevice(Guid deviceId)
    {
        lock (_gate)
        {
            _configured.Remove(deviceId);
            _connections.Remove(deviceId);
            foreach (var key in _values.Keys.Where(key => key.DeviceId == deviceId).ToArray()) _values.Remove(key);
        }
    }

    public OpcTagMonitorSnapshot Snapshot(DateTimeOffset now)
    {
        lock (_gate) return new(now, options.Value.NoDataSeconds, _values.Values.ToArray(), new Dictionary<Guid, string>(_connections));
    }
}
