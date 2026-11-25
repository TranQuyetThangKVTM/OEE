namespace Oee.Application.PlcIntegration;

/// <summary>Session OPC UA bất đồng bộ; DisposeAsync giải phóng kết nối và subscription.</summary>
public interface IOpcUaSession : IAsyncDisposable
{
    /// <summary>Trạng thái transport/session hiện tại.</summary>
    bool IsConnected
    {
        get;
    }

    /// <summary>Duyệt node con của parent phục vụ dialog browse tag.</summary>
    Task<IReadOnlyCollection<OpcUaNodeDescriptor>> BrowseAsync(
        string? parentNodeId,
        CancellationToken cancellationToken);

    /// <summary>Đọc một nhóm tag trong một round-trip.</summary>
    Task<IReadOnlyCollection<OpcUaValueSample>> ReadAsync(
        IReadOnlyCollection<OpcUaReadRequest> requests,
        CancellationToken cancellationToken);

    /// <summary>Ghi một nhóm output tag khi connection khỏe.</summary>
    Task WriteAsync(
        IReadOnlyCollection<OpcUaWriteRequest> requests,
        CancellationToken cancellationToken);

    /// <summary>Đăng ký callback subscription thay cho polling dày đặc.</summary>
    Task<IOpcUaSubscription> SubscribeAsync(
        IReadOnlyCollection<OpcUaReadRequest> requests,
        Func<OpcUaValueSample, CancellationToken, ValueTask> onSample,
        CancellationToken cancellationToken);
}
