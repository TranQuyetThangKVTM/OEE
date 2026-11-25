using System.Collections.Concurrent;
using Oee.Application.PlcIntegration;

namespace Oee.Infrastructure.Integrations.OpcUa;

/// <summary>
/// Adapter OPC UA giả, thread-safe, dùng cho Development và test mà không cần PLC thật.
/// </summary>
/// <remarks>Mỗi ConnectionId có một session riêng để test reconnect, read/write và subscription độc lập.</remarks>
public sealed class FakeOpcUaAdapter : IOpcUaClientAdapter
{
    private readonly ConcurrentDictionary<Guid, FakeOpcUaSession> sessions = new();
    /// <summary>Kết nối logic tới fake session đã có hoặc tạo session mới cho connection.</summary>
    public Task<IOpcUaSession> ConnectAsync(OpcUaEndpointOptions endpoint, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IOpcUaSession>(sessions.GetOrAdd(endpoint.ConnectionId, _ => new FakeOpcUaSession()));
    }

    /// <summary>Cho test lấy trực tiếp session để publish sample mô phỏng từ PLC.</summary>
    public FakeOpcUaSession GetSession(Guid connectionId) => sessions.GetOrAdd(connectionId, _ => new FakeOpcUaSession());
}
