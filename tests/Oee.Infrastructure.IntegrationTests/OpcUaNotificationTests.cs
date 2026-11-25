using Oee.Domain.Common;
using Oee.Infrastructure.Integrations.OpcUa;
using Opc.Ua;

namespace Oee.Infrastructure.IntegrationTests;

public sealed class OpcUaNotificationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SingleSdkNotificationPreservesCounterSourceTimeAndQuality(bool bad)
    {
        var timestamp = new DateTime(2026, 10, 12, 16, 29, 0, DateTimeKind.Utc);
        var payload = new MonitoredItemNotification
        {
            Value = new DataValue(new Variant(335616u))
            {
                SourceTimestamp = timestamp,
                StatusCode = bad ? StatusCodes.BadNoCommunication : StatusCodes.Good,
            },
        };
        var sample = OpcUaSdkAdapter.DecodeNotification(new("ns=8;s=Total", TagRoleEnum.TotalCounter), payload);
        Assert.NotNull(sample);
        Assert.Equal(335616u, sample.Value);
        Assert.Equal("ns=8;s=Total", sample.NodeId);
        Assert.Equal(TagRoleEnum.TotalCounter, sample.Role);
        Assert.Equal(new DateTimeOffset(timestamp), sample.SourceTimestamp);
        Assert.Equal(!bad, sample.IsGood);
    }
}
