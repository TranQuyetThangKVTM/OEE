using Microsoft.Extensions.Options;
using Oee.Application.PlcIntegration;
using Oee.Domain.Common;
using Oee.Infrastructure.BackgroundServices.Configuration;
using Oee.Infrastructure.PlcIntegration;

namespace Oee.Infrastructure.IntegrationTests;

public sealed class OpcTagMonitorBufferTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 2, 0, 0, TimeSpan.Zero);
    private readonly OpcTagMonitorBuffer _buffer = new(Options.Create(new OpcUaWorkerOptions()));
    private readonly Guid _device = Guid.NewGuid();
    private const string Node = "ns=2;s=Total";

    [Fact]
    public void UnchangedValueWithOldSourceTimeStillRefreshesReceptionAndDoesNotClaimChange()
    {
        Configure();
        _buffer.Record(_device, Sample(100), Now, true);
        _buffer.Record(_device, Sample(100), Now.AddSeconds(10), true);
        var value = Assert.Single(_buffer.Snapshot(Now.AddSeconds(10)).Values);
        Assert.Equal(Now.AddDays(-10), value.SourceAt);
        Assert.Equal(Now.AddSeconds(10), value.ReceivedAt);
        Assert.Null(value.ChangedAt);
        Assert.True(value.IsGood);
    }

    [Fact]
    public void ChangeTimeQualityAndSnapshotAreIndependentFromCheckpoint()
    {
        Configure();
        _buffer.Record(_device, Sample(100), Now, true);
        var before = _buffer.Snapshot(Now);
        _buffer.Record(_device, Sample(101), Now.AddSeconds(1), false);
        _buffer.Record(_device, Sample(101) with { IsGood = false, StatusCode = "BadNoCommunication" }, Now.AddSeconds(2), false);
        var value = Assert.Single(_buffer.Snapshot(Now.AddSeconds(2)).Values);
        Assert.Equal(Now.AddSeconds(1), value.ChangedAt);
        Assert.False(value.IsGood);
        Assert.Equal("BadNoCommunication", value.StatusCode);
        Assert.Equal("100", Assert.Single(before.Values).Value);
    }

    [Fact]
    public void NullGoodValueIsReportedAsProblemAndLateReceptionDoesNotOverwrite()
    {
        Configure();
        _buffer.Record(_device, Sample(101) with { Value = null }, Now, false);
        _buffer.Record(_device, Sample(100), Now.AddSeconds(-1), true);
        var value = Assert.Single(_buffer.Snapshot(Now).Values);
        Assert.False(value.IsGood);
        Assert.Equal("—", value.Value);
    }

    [Fact]
    public void ReconfigurationAndRemovalDiscardObsoleteTagsAndLateCallbacks()
    {
        Configure();
        _buffer.Record(_device, Sample(100), Now, true);
        _buffer.Configure(_device, [new("ns=2;s=New", TagRoleEnum.TotalCounter)]);
        _buffer.Record(_device, Sample(101), Now.AddSeconds(1), false);
        Assert.Empty(_buffer.Snapshot(Now).Values);
        _buffer.Record(_device, Sample(102) with { NodeId = "ns=2;s=New" }, Now, false);
        _buffer.SetConnection(_device, "Connected");
        _buffer.RemoveDevice(_device);
        _buffer.Record(_device, Sample(103) with { NodeId = "ns=2;s=New" }, Now, false);
        Assert.Empty(_buffer.Snapshot(Now).Values);
        Assert.Empty(_buffer.Snapshot(Now).Connections);
    }

    [Fact]
    public async Task ParallelDeviceCallbacksAndReadersHaveStableSnapshots()
    {
        var devices = Enumerable.Range(0, 40).Select(_ => Guid.NewGuid()).ToArray();
        await Task.WhenAll(devices.Select(device => Task.Run(() =>
        {
            _buffer.Configure(device, [new(Node, TagRoleEnum.TotalCounter)]);
            for (var i = 0; i < 30; i++)
            {
                _buffer.Record(device, Sample(i), Now.AddMilliseconds(i), false);
                _ = _buffer.Snapshot(Now);
            }
        })));
        Assert.Equal(40, _buffer.Snapshot(Now).Values.Count);
        Assert.All(_buffer.Snapshot(Now).Values, value => Assert.Equal("29", value.Value));
    }

    private void Configure() => _buffer.Configure(_device, [new(Node, TagRoleEnum.TotalCounter)]);
    private static OpcUaValueSample Sample(decimal value) => new(Node, TagRoleEnum.TotalCounter, value, Now.AddDays(-10), true, "Good");
}
