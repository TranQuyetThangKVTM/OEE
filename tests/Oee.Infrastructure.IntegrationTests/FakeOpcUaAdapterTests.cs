using Oee.Application.PlcIntegration;
using Oee.Domain.Common;
using Oee.Infrastructure.Integrations.OpcUa;

namespace Oee.Infrastructure.IntegrationTests;

/// <summary>
/// Integration Test cho Fake OPC UA adapter dùng trong CI/UAT khi không có PLC thật.
/// </summary>
public sealed class FakeOpcUaAdapterTests
{
    [Fact]
    public async Task FakeSupportsSubscribeReadWriteAndBrowse()
    {
        // Arrange: kết nối một session giả và đăng ký callback cho tag TotalCounter.
        var adapter = new FakeOpcUaAdapter(); var connectionId = Guid.NewGuid();
        await using var session = await adapter.ConnectAsync(new(connectionId, "opc.tcp://fake", "None", "None", "Anonymous", null, null, null), CancellationToken.None);
        var received = new TaskCompletionSource<OpcUaValueSample>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var subscription = await session.SubscribeAsync([new("ns=2;s=Total", TagRoleEnum.TotalCounter)], (sample, _) => { received.TrySetResult(sample); return ValueTask.CompletedTask; }, CancellationToken.None);
        // Act: giả lập PLC publish giá trị, ghi output và browse các NodeId đã biết.
        await adapter.GetSession(connectionId).PublishAsync(new("ns=2;s=Total", TagRoleEnum.TotalCounter, 42m, DateTimeOffset.UtcNow, true, null));
        // Assert: subscription nhận đúng mẫu và node vừa ghi xuất hiện trong browse result.
        Assert.Equal(42m, (decimal)(await received.Task).Value!);
        await session.WriteAsync([new("ns=2;s=Output", 85m)], CancellationToken.None);
        Assert.Contains(await session.BrowseAsync(null, CancellationToken.None), node => node.NodeId == "ns=2;s=Output");
    }
}
