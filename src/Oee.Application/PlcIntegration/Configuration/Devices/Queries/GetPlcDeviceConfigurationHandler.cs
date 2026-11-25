using Oee.Application.Common;
using Oee.Application.Contracts.PlcIntegration;

namespace Oee.Application.PlcIntegration.Configuration.Devices.Queries;

/// <summary>Điều phối thao tác GetPlcDeviceConfiguration trong cùng scope lưu dữ liệu.</summary>
internal sealed class GetPlcDeviceConfigurationHandler(IPlcConfigurationReadStore reads)
    : IRequestHandler<GetPlcDeviceConfigurationQuery, PlcDeviceConfiguration>
{
    public Task<PlcDeviceConfiguration> HandleAsync(
        GetPlcDeviceConfigurationQuery command,
        CancellationToken cancellationToken)
    {
        var id = command.Id;

        return reads.DeviceConfigurationAsync(id, cancellationToken);
    }
}
