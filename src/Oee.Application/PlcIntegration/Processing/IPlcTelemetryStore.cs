using Oee.Domain.Downtime;
using Oee.Domain.Entities.Production;
using Oee.Domain.Platform;
using Oee.Domain.PlcIntegration;

namespace Oee.Application.PlcIntegration;

public interface IPlcTelemetryStore
{
    Task<PlcDeviceMappingEntity?> FindDeviceAsync(Guid id, CancellationToken ct);

    Task<CounterCheckpointEntity?> FindCheckpointAsync(Guid deviceId, CancellationToken ct);

    Task<PlcDeviceRuntimeStateEntity?> FindRuntimeAsync(Guid deviceId, CancellationToken ct);

    Task<DowntimeEventEntity?> FindOpenDowntimeAsync(Guid unitId, CancellationToken ct);

    Task<Guid?> FindFaultReasonAsync(
        Guid unitId,
        string? faultCode,
        CancellationToken ct);

    Task<Guid?> FindUnknownReasonAsync(CancellationToken ct);

    Task<bool> HasFaultMappingAsync(
        Guid unitId,
        string faultCode,
        CancellationToken ct);

    Task<ProductionPlanEntity?> FindPlanAtAsync(
        Guid unitId,
        DateTimeOffset producedAt,
        CancellationToken ct);

    void AddCheckpoint(CounterCheckpointEntity checkpoint);

    void AddRuntime(PlcDeviceRuntimeStateEntity runtime);

    void AddOutbox(OutboxMessageEntity message);

    void AddUnplannedDelta(ProductionReconciliationItemEntity item);

    void AddEvent(PlcIntegrationEventEntity item);
}
