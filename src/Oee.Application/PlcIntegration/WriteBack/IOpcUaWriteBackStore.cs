using Oee.Domain.Entities.Oee;
using Oee.Domain.PlcIntegration;

namespace Oee.Application.PlcIntegration;

public interface IOpcUaWriteBackStore
{
    Task<PlcDeviceMappingEntity[]> ReadDevicesAsync(CancellationToken ct);

    Task<PlcDeviceRuntimeStateEntity?> FindRuntimeAsync(Guid deviceId, CancellationToken ct);

    Task<OpcUaTagMappingEntity[]> ReadWriteTagsAsync(Guid deviceId, CancellationToken ct);

    Task<OeeSnapshotEntity?> FindLatestSnapshotAsync(Guid unitId, CancellationToken ct);

    Task<decimal?> ReadCurrentTargetAsync(
        Guid unitId,
        DateTimeOffset now,
        CancellationToken ct);

    Task<DateTimeOffset?> ReadOpenDowntimeStartAsync(Guid unitId, CancellationToken ct);
}
