using System.Collections.Concurrent;
using Oee.Application.PlcIntegration;
using Oee.Domain.Common;
using Oee.Infrastructure.BackgroundServices;
using Oee.Infrastructure.Integrations.OpcUa;

namespace Oee.Infrastructure.IntegrationTests;

public sealed class OpcUaConnectionPumpTests
{
    [Fact]
    public async Task SharedSubscriptionDeliversAtomicInitialBatchesAndRoutesOnlyMatchingDevice()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var events = new ConcurrentQueue<(Guid Id, OpcUaValueSample[] Samples, bool Initial)>();
        await using var session = new RecordingSession();
        using var stop = new CancellationTokenSource();
        var pump = new OpcUaConnectionPump([
            new(first, [new("run", TagRoleEnum.Running), new("total-1", TagRoleEnum.TotalCounter)]),
            new(second, [new("run", TagRoleEnum.Running), new("total-2", TagRoleEnum.TotalCounter)])]);
        var running = pump.RunAsync(session, (id, samples, initial, _, _) =>
        {
            events.Enqueue((id, samples.ToArray(), initial));
            return ValueTask.CompletedTask;
        }, _ => Task.CompletedTask, TimeSpan.FromSeconds(10), stop.Token);
        await session.Subscribed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(1, session.SubscriptionCount);
        Assert.Equal(3, session.Requests.Count);
        Assert.Equal(2, events.Count);
        Assert.All(events, item => { Assert.True(item.Initial); Assert.Equal(2, item.Samples.Length); });
        events.Clear();
        await session.PublishAsync(new("total-1", TagRoleEnum.TotalCounter, 2861m, DateTimeOffset.UtcNow, true, "Good"));
        var received = Assert.Single(events);
        Assert.Equal(first, received.Id);
        Assert.False(received.Initial);
        Assert.Equal(2861m, Assert.Single(received.Samples).Value);
        await stop.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
        Assert.True(session.SubscriptionDisposed);
    }

    [Fact]
    public async Task SameNodeAndDifferentRoleDoesNotLeakIntoOtherDevice()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var received = new ConcurrentQueue<Guid>();
        await using var session = new RecordingSession();
        using var stop = new CancellationTokenSource();
        var pump = new OpcUaConnectionPump([
            new(first, [new("shared", TagRoleEnum.Running)]),
            new(second, [new("shared", TagRoleEnum.TotalCounter)])]);
        var running = pump.RunAsync(session, (id, _, _, _, _) =>
        {
            received.Enqueue(id);
            return ValueTask.CompletedTask;
        }, _ => Task.CompletedTask, TimeSpan.FromSeconds(10), stop.Token);
        await session.Subscribed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        received.Clear();
        await session.PublishAsync(new("shared", TagRoleEnum.TotalCounter, 10m, DateTimeOffset.UtcNow, true, "Good"));
        Assert.Equal(second, Assert.Single(received));
        await stop.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
    }

    [Fact]
    public async Task ReadRefreshKeepsRunningWithoutBrowserOrSubscriptionNotifications()
    {
        await using var session = new RecordingSession();
        using var stop = new CancellationTokenSource();
        var refreshed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pump = new OpcUaConnectionPump([new(Guid.NewGuid(), [new("total", TagRoleEnum.TotalCounter)])]);
        var running = pump.RunAsync(session, (_, _, initial, readRefresh, _) =>
        {
            if (!initial && readRefresh) refreshed.TrySetResult();
            return ValueTask.CompletedTask;
        }, _ => Task.CompletedTask, TimeSpan.FromMilliseconds(10), stop.Token);
        await refreshed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(session.ReadCount >= 2);
        await stop.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
    }

    [Fact]
    public async Task HungReadWithoutCancellationSupportTimesOutInsteadOfBlockingTheConnectionLoop()
    {
        var session = new RecordingSession { HangReads = true };
        var pump = new OpcUaConnectionPump([new(Guid.NewGuid(), [new("total", TagRoleEnum.TotalCounter)])]);
        var running = pump.RunAsync(session, (_, _, _, _, _) => ValueTask.CompletedTask,
            _ => Task.CompletedTask, TimeSpan.FromMilliseconds(10), default, TimeSpan.FromMilliseconds(30));

        await Assert.ThrowsAsync<TimeoutException>(() => running.WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.Equal(0, session.SubscriptionCount);
    }

    [Fact]
    public void KeepAliveRequiresTwoConsecutiveBadResultsAndKeepsReconnectRequestSticky()
    {
        var state = new OpcUaKeepAliveState();
        state.Observe(true);
        Assert.False(state.ReconnectRequested);
        state.Observe(false);
        state.Observe(true);
        Assert.False(state.ReconnectRequested);
        state.Observe(true);
        Assert.True(state.ReconnectRequested);
        state.Observe(false);
        Assert.True(state.ReconnectRequested);
    }

    [Theory]
    [InlineData(TagRoleEnum.Running, 100)]
    [InlineData(TagRoleEnum.FaultCode, 200)]
    [InlineData(TagRoleEnum.TotalCounter, 1000)]
    [InlineData(TagRoleEnum.ProductCode, 5000)]
    public void SamplingUsesImportedSourceIntervals(TagRoleEnum role, int expected) =>
        Assert.Equal(expected, OpcUaSdkSession.SamplingInterval(role));

    private sealed class RecordingSession : IOpcUaSession
    {
        private Func<OpcUaValueSample, CancellationToken, ValueTask>? callback;
        public TaskCompletionSource Subscribed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int SubscriptionCount { get; private set; }
        public int ReadCount { get; private set; }
        public IReadOnlyCollection<OpcUaReadRequest> Requests { get; private set; } = [];
        public bool SubscriptionDisposed { get; private set; }
        public bool IsConnected => true;
        public bool HangReads { get; init; }
        public Task<IReadOnlyCollection<OpcUaValueSample>> ReadAsync(IReadOnlyCollection<OpcUaReadRequest> requests, CancellationToken cancellationToken)
        {
            ReadCount++;
            if (HangReads) return new TaskCompletionSource<IReadOnlyCollection<OpcUaValueSample>>(TaskCreationOptions.RunContinuationsAsynchronously).Task;
            return Task.FromResult<IReadOnlyCollection<OpcUaValueSample>>(requests.Select(request =>
                new OpcUaValueSample(request.NodeId, request.Role, 100m, DateTimeOffset.UtcNow, true, "Good")).ToArray());
        }
        public Task<IOpcUaSubscription> SubscribeAsync(IReadOnlyCollection<OpcUaReadRequest> requests,
            Func<OpcUaValueSample, CancellationToken, ValueTask> onSample, CancellationToken cancellationToken)
        {
            Requests = requests;
            callback = onSample;
            SubscriptionCount++;
            Subscribed.TrySetResult();
            return Task.FromResult<IOpcUaSubscription>(new SubscriptionHandle(() => SubscriptionDisposed = true));
        }
        public ValueTask PublishAsync(OpcUaValueSample sample) => callback!(sample, CancellationToken.None);
        public Task<IReadOnlyCollection<OpcUaNodeDescriptor>> BrowseAsync(string? parentNodeId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task WriteAsync(IReadOnlyCollection<OpcUaWriteRequest> requests, CancellationToken cancellationToken) => throw new NotSupportedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class SubscriptionHandle(Action dispose) : IOpcUaSubscription
    {
        public ValueTask DisposeAsync() { dispose(); return ValueTask.CompletedTask; }
    }
}
