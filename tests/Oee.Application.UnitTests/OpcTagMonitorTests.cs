using Oee.Application.Common;
using Oee.Application.PlcIntegration;
using Oee.Application.PlcIntegration.Monitoring;
using Oee.Application.Security;
using Oee.Domain.Common;

namespace Oee.Application.UnitTests;

public sealed class OpcTagMonitorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task DirectReadUsesOnlyStoredTagAndNeverChangesSubscriptionBufferOrProduction()
    {
        await using var fixture = new Fixture();
        var result = await fixture.Execute(new ReadOpcTagDirectQuery(fixture.Mapping.Id, fixture.Mapping.NodeId));
        Assert.True(result.IsGood);
        Assert.Equal("123", result.Value);
        Assert.Equal(Now.AddDays(-1), result.SourceAt);
        Assert.Equal(Now, result.ReadAt);
        Assert.Equal(fixture.Mapping.NodeId, Assert.Single(fixture.Adapter.Reads!).NodeId);
        Assert.Equal(0, fixture.Buffer.Writes);
        Assert.True(fixture.Adapter.Disposed);
    }

    [Fact]
    public async Task OutputTagsAreAlsoReadOnlyAndAreNotSubscribedByDirectRead()
    {
        await using var fixture = new Fixture();
        fixture.Mapping = fixture.Mapping with { Direction = TagDirectionEnum.Write, Role = TagRoleEnum.Oee };
        var result = await fixture.Execute(new ReadOpcTagDirectQuery(fixture.Mapping.Id, fixture.Mapping.NodeId));
        Assert.True(result.IsGood);
        Assert.Equal(TagRoleEnum.Oee, Assert.Single(fixture.Adapter.Reads!).Role);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("disabled")]
    [InlineData("changed")]
    public async Task RemovedDisabledOrChangedTagsDoNotOpenConnection(string reason)
    {
        await using var fixture = new Fixture();
        fixture.Missing = reason == "missing";
        fixture.Mapping = fixture.Mapping with { Enabled = reason != "disabled" };
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Execute(new ReadOpcTagDirectQuery(fixture.Mapping.Id,
            reason == "changed" ? "arbitrary-node" : fixture.Mapping.NodeId)));
        Assert.Equal(0, fixture.Adapter.Connects);
    }

    [Fact]
    public async Task AdapterFailureIsSanitizedAndBadQualityIsNotConvertedToGood()
    {
        await using var fixture = new Fixture();
        fixture.Adapter.Fail = true;
        var failed = await fixture.Execute(new ReadOpcTagDirectQuery(fixture.Mapping.Id, fixture.Mapping.NodeId));
        Assert.False(failed.IsGood);
        Assert.DoesNotContain("secret-password", failed.StatusCode, StringComparison.Ordinal);
        fixture.Adapter.Fail = false;
        fixture.Adapter.Good = false;
        var bad = await fixture.Execute(new ReadOpcTagDirectQuery(fixture.Mapping.Id, fixture.Mapping.NodeId));
        Assert.False(bad.IsGood);
        Assert.Equal("BadNoCommunication", bad.StatusCode);
    }

    [Fact]
    public async Task SnapshotIsMemoryOnlyAndDoesNotQueryMappingsOrOpenOpcConnections()
    {
        await using var fixture = new Fixture();
        var snapshot = await fixture.Execute(new GetOpcTagMonitorSnapshotQuery());
        Assert.Equal(Now, snapshot.At);
        Assert.Equal(0, fixture.Lists);
        Assert.Equal(0, fixture.Adapter.Connects);
    }

    [Fact]
    public async Task SignedOutReaderIsRejectedBeforeAnyPortsAreUsed()
    {
        await using var fixture = new Fixture { Allowed = false };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Execute(new ListOpcTagMonitorMappingsQuery()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Execute(new GetOpcTagMonitorSnapshotQuery()));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Execute(new ReadOpcTagDirectQuery(fixture.Mapping.Id, fixture.Mapping.NodeId)));
        Assert.Equal(0, fixture.Lists);
        Assert.Equal(0, fixture.Adapter.Connects);
    }

    [Fact]
    public async Task CallerCancellationIsPropagatedInsteadOfReturningNetworkError()
    {
        await using var fixture = new Fixture();
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Execute(
            new ReadOpcTagDirectQuery(fixture.Mapping.Id, fixture.Mapping.NodeId), source.Token));
    }

    private sealed class Fixture : IOpcTagMonitorStore, IClock, ICapabilityChecker, IAsyncDisposable
    {
        public OpcTagMonitorMapping Mapping = new(Guid.NewGuid(), Guid.NewGuid(), "M01", "Machine", "OPC", "ns=2;s=Total",
            TagRoleEnum.TotalCounter, TagDirectionEnum.Read, "Double", true);
        public readonly Adapter Adapter = new();
        public readonly Buffer Buffer = new();
        public bool Missing;
        public bool Allowed = true;
        public int Lists;
        public DateTimeOffset UtcNow => Now;
        public ValueTask DisposeAsync() => Adapter.DisposeAsync();
        public bool HasCapability(string capability) => Allowed && capability == Capabilities.MasterDataRead;
        public Task<IReadOnlyCollection<OpcTagMonitorMapping>> ListAsync(CancellationToken ct)
        { Lists++; return Task.FromResult<IReadOnlyCollection<OpcTagMonitorMapping>>([Mapping]); }
        public Task<OpcTagMonitorTarget?> FindAsync(Guid tagId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(Missing ? null : new OpcTagMonitorTarget(Mapping,
                new(Guid.NewGuid(), "opc.tcp://test", "None", "None", "Anonymous", null, null, null)));
        }
        public Task<T> Execute<T>(IRequest<T> request, CancellationToken ct = default) => new ScenarioHandlerExecutor(new Dictionary<Type, object?>
        {
            [typeof(IOpcTagMonitorStore)] = this, [typeof(IOpcTagMonitorBuffer)] = Buffer,
            [typeof(IOpcUaClientAdapter)] = Adapter, [typeof(IClock)] = this, [typeof(ICapabilityChecker)] = this
        }).ExecuteAsync(request, ct);
    }

    private sealed class Adapter : IOpcUaClientAdapter, IOpcUaSession
    {
        public bool Fail;
        public bool Good = true;
        public int Connects;
        public bool Disposed;
        public IReadOnlyCollection<OpcUaReadRequest>? Reads;
        public bool IsConnected => true;
        public Task<IOpcUaSession> ConnectAsync(OpcUaEndpointOptions endpoint, CancellationToken cancellationToken)
        { Connects++; if (Fail) throw new InvalidOperationException("secret-password"); return Task.FromResult<IOpcUaSession>(this); }
        public Task<IReadOnlyCollection<OpcUaValueSample>> ReadAsync(IReadOnlyCollection<OpcUaReadRequest> requests, CancellationToken cancellationToken)
        {
            Reads = requests;
            return Task.FromResult<IReadOnlyCollection<OpcUaValueSample>>(requests.Select(read => new OpcUaValueSample(read.NodeId,
                read.Role, 123m, Now.AddDays(-1), Good, Good ? "Good" : "BadNoCommunication")).ToArray());
        }
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
        public Task WriteAsync(IReadOnlyCollection<OpcUaWriteRequest> requests, CancellationToken cancellationToken) => throw new InvalidOperationException("Monitor must never write");
        public Task<IReadOnlyCollection<OpcUaNodeDescriptor>> BrowseAsync(string? parentNodeId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IOpcUaSubscription> SubscribeAsync(IReadOnlyCollection<OpcUaReadRequest> requests, Func<OpcUaValueSample, CancellationToken, ValueTask> onSample, CancellationToken cancellationToken) => throw new InvalidOperationException("Direct read must not subscribe");
    }

    private sealed class Buffer : IOpcTagMonitorBuffer
    {
        public int Writes;
        public void Record(Guid deviceId, OpcUaValueSample sample, DateTimeOffset receivedAt, bool fromReadRefresh) => Writes++;
        public void Configure(Guid deviceId, IReadOnlyCollection<OpcUaReadRequest> reads) => Writes++;
        public void SetConnection(Guid deviceId, string state) => Writes++;
        public void RemoveDevice(Guid deviceId) => Writes++;
        public OpcTagMonitorSnapshot Snapshot(DateTimeOffset now) => new(now, 30, [], new Dictionary<Guid, string>());
    }
}
