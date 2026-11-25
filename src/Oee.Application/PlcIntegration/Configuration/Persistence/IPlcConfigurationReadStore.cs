using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.PlcIntegration;

namespace Oee.Application.PlcIntegration;

public interface IPlcConfigurationReadStore
{
    Task<PlcConfigurationLookups> LookupsAsync(CancellationToken ct);

    Task<PagedResult<OpcUaConnectionItem>> ConnectionsAsync(PlcListQuery query, CancellationToken ct);

    Task<PagedResult<PlcDeviceMappingItem>> DeviceMappingsAsync(
        PlcListQuery query,
        CancellationToken ct);

    Task<PlcDeviceConfiguration> DeviceConfigurationAsync(Guid id, CancellationToken ct);

    Task<PagedResult<PlcFaultMappingItem>> FaultMappingsAsync(PlcListQuery query, CancellationToken ct);
}
