using Oee.Application.Audit;

namespace Oee.Application.UnitTests;

public sealed class AuditIpAddressTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData(" ", null)]
    [InlineData("unknown", null)]
    [InlineData("192.168.1.2, 10.0.0.1", null)]
    [InlineData("192.168.1.2", "192.168.1.2")]
    [InlineData("::ffff:192.168.1.2", "192.168.1.2")]
    [InlineData("::1", "::1")]
    [InlineData("2001:0db8:0000:0000:0000:0000:0000:0001", "2001:db8::1")]
    public void AuditAddressPreservesValidAddressesAndIgnoresInvalidValues(string? input, string? expected)
    {
        Assert.Equal(expected, AuditIpAddress.Normalize(input));
    }
}
