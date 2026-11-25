using System.Collections.Concurrent;
using Oee.Application.PlcIntegration;

namespace Oee.Infrastructure.Integrations.OpcUa;

/// <summary>Session in-memory mô phỏng browse, read, write và subscription của OPC UA.</summary>
public sealed class FakeOpcUaSession : IOpcUaSession
{
    private readonly ConcurrentDictionary<string, object?> values = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTimeOffset> timestamps = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Guid, (HashSet<string> Nodes, Func<OpcUaValueSample, CancellationToken, ValueTask> Callback)> subscriptions = new();
    /// <summary>Cho biết session giả còn mở; DisposeAsync chuyển trạng thái sang disconnected.</summary>
    public bool IsConnected { get; private set; } = true;

    /// <summary>Liệt kê các NodeId đã từng được read/write/publish trong session.</summary>
    public Task<IReadOnlyCollection<OpcUaNodeDescriptor>> BrowseAsync(string? parentNodeId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<OpcUaNodeDescriptor>>(values.Keys.Select(x => new OpcUaNodeDescriptor(x, x, null, false)).ToArray());

    /// <summary>Đọc snapshot hiện tại của các node và trả sample có source timestamp UTC.</summary>
    public Task<IReadOnlyCollection<OpcUaValueSample>> ReadAsync(IReadOnlyCollection<OpcUaReadRequest> requests, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyCollection<OpcUaValueSample>>(requests.Select(x => new OpcUaValueSample(x.NodeId, x.Role, values.GetValueOrDefault(x.NodeId), timestamps.GetValueOrDefault(x.NodeId, DateTimeOffset.UtcNow), true, null)).ToArray());

    /// <summary>Mô phỏng write-back bằng cách cập nhật dictionary trong bộ nhớ.</summary>
    public Task WriteAsync(IReadOnlyCollection<OpcUaWriteRequest> requests, CancellationToken cancellationToken) { foreach (var item in requests) { values[item.NodeId] = item.Value; timestamps[item.NodeId] = DateTimeOffset.UtcNow; } return Task.CompletedTask; }

    /// <summary>Đăng ký callback cho tập NodeId và trả handle dùng để hủy subscription.</summary>
    public Task<IOpcUaSubscription> SubscribeAsync(IReadOnlyCollection<OpcUaReadRequest> requests, Func<OpcUaValueSample, CancellationToken, ValueTask> onSample, CancellationToken cancellationToken)
    { var id = Guid.NewGuid(); subscriptions[id] = (requests.Select(x => x.NodeId).ToHashSet(StringComparer.Ordinal), onSample); return Task.FromResult<IOpcUaSubscription>(new FakeOpcUaSubscription(() => subscriptions.TryRemove(id, out _))); }

    /// <summary>Phát sample giả tới mọi subscription có theo dõi đúng NodeId.</summary>
    public async Task PublishAsync(OpcUaValueSample sample, CancellationToken cancellationToken = default) { values[sample.NodeId] = sample.Value; timestamps[sample.NodeId] = sample.SourceTimestamp; foreach (var item in subscriptions.Values.Where(x => x.Nodes.Contains(sample.NodeId))) await item.Callback(sample, cancellationToken); }

    /// <summary>Đóng session và giải phóng toàn bộ callback đã đăng ký.</summary>
    public ValueTask DisposeAsync() { IsConnected = false; subscriptions.Clear(); return ValueTask.CompletedTask; }
}
