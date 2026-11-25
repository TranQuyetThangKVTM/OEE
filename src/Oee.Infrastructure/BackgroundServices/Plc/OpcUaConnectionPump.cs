using Oee.Application.PlcIntegration;

namespace Oee.Infrastructure.BackgroundServices;

/// <summary>Một session/subscription cho các thiết bị dùng chung kết nối, như source OPC gốc.</summary>
internal sealed class OpcUaConnectionPump(IReadOnlyCollection<OpcUaDeviceInput> devices)
{
    private readonly OpcUaReadRequest[] requests = devices.SelectMany(device => device.Reads)
        .DistinctBy(request => (request.NodeId, request.Role)).ToArray();

    internal async Task RunAsync(
        IOpcUaSession session,
        Func<Guid, IReadOnlyCollection<OpcUaValueSample>, bool, bool, CancellationToken, ValueTask> receive,
        Func<CancellationToken, Task> checkHealth,
        TimeSpan refreshInterval,
        CancellationToken cancellationToken,
        TimeSpan? readTimeout = null)
    {
        await DispatchAsync(await ReadFreshAsync(), true, true, cancellationToken);
        await using var subscription = await session.SubscribeAsync(requests,
            (sample, token) => DispatchAsync([sample], false, false, token), cancellationToken);
        await checkHealth(cancellationToken);

        while (session.IsConnected)
        {
            await Task.Delay(refreshInterval, cancellationToken);
            await DispatchAsync(await ReadFreshAsync(), false, true, cancellationToken);
            await checkHealth(cancellationToken);
        }

        throw new InvalidOperationException("OPC UA session disconnected.");

        async Task<IReadOnlyCollection<OpcUaValueSample>> ReadFreshAsync()
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(readTimeout ?? TimeSpan.FromSeconds(15));
            try { return await session.ReadAsync(requests, timeout.Token).WaitAsync(timeout.Token); }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("OPC UA direct read timed out; reconnect the session.", exception);
            }
        }

        async ValueTask DispatchAsync(IReadOnlyCollection<OpcUaValueSample> samples, bool initial, bool readRefresh, CancellationToken token)
        {
            foreach (var device in devices)
            {
                // Một NodeId có thể được nhiều thiết bị dùng chung; không để sample lẫn role hoặc thiết bị.
                var matching = samples.Where(sample => device.Reads.Any(read =>
                    read.NodeId == sample.NodeId && read.Role == sample.Role)).ToArray();
                if (matching.Length > 0)
                    await receive(device.Id, matching, initial, readRefresh, token);
            }
        }
    }
}

internal sealed record OpcUaDeviceInput(Guid Id, IReadOnlyCollection<OpcUaReadRequest> Reads);
