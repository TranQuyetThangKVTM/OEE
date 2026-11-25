using System.Linq.Expressions;
using Oee.Domain.PlcIntegration;

namespace Oee.Application.PlcIntegration;

public interface IPlcConfigurationStore
{
    Task<OpcUaConnectionEntity?> FindConnectionAsync(
        Guid id,
        bool tracking,
        CancellationToken ct);

    Task<PlcDeviceMappingEntity?> FindDeviceAsync(Guid id, CancellationToken ct);

    Task<PlcFaultCodeMappingEntity?> FindFaultAsync(Guid id, CancellationToken ct);

    Task<OpcUaTagMappingEntity[]> ReadTagsAsync(Guid deviceId, CancellationToken ct);

    Task<bool> AnyConnectionsAsync(
        Expression<Func<OpcUaConnectionEntity, bool>> predicate,
        CancellationToken ct);

    Task<bool> AnyDevicesAsync(
        Expression<Func<PlcDeviceMappingEntity, bool>> predicate,
        CancellationToken ct);

    Task<bool> AnyFaultsAsync(
        Expression<Func<PlcFaultCodeMappingEntity, bool>> predicate,
        CancellationToken ct);

    void AddConnection(OpcUaConnectionEntity connection);

    void AddDevice(PlcDeviceMappingEntity device);

    void AddTag(OpcUaTagMappingEntity tag);

    void AddFault(PlcFaultCodeMappingEntity fault);

}
