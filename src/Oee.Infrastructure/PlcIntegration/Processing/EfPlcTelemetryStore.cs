using Microsoft.EntityFrameworkCore;
using Oee.Application.PlcIntegration;
using Oee.Domain.Downtime;
using Oee.Domain.Entities.Production;
using Oee.Domain.Platform;
using Oee.Domain.PlcIntegration;
using Oee.Infrastructure.Persistence;

namespace Oee.Infrastructure.PlcIntegration;

public sealed class EfPlcTelemetryStore(EfApplicationUnitOfWork unitOfWork) : IPlcTelemetryStore
{
    private OeeDbContext Db => unitOfWork.Db;
    public Task<PlcDeviceMappingEntity?> FindDeviceAsync(Guid id, CancellationToken ct) => Db.PlcDeviceMappings.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<CounterCheckpointEntity?> FindCheckpointAsync(Guid deviceId, CancellationToken ct) => Db.CounterCheckpoints.SingleOrDefaultAsync(x => x.PlcDeviceMappingId == deviceId, ct);
    public Task<PlcDeviceRuntimeStateEntity?> FindRuntimeAsync(Guid deviceId, CancellationToken ct) => Db.PlcDeviceRuntimeStates.SingleOrDefaultAsync(x => x.PlcDeviceMappingId == deviceId, ct);
    public Task<DowntimeEventEntity?> FindOpenDowntimeAsync(Guid unitId, CancellationToken ct) => Db.DowntimeEvents.SingleOrDefaultAsync(x => x.WorkUnitId == unitId && x.EndAt == null, ct);
    public Task<Guid?> FindFaultReasonAsync(Guid unitId, string? faultCode, CancellationToken ct) => Db.PlcFaultCodeMappings.Where(x => x.IsActive && x.FaultCode == faultCode && (x.WorkUnitId == unitId || x.WorkUnitId == null)).OrderByDescending(x => x.WorkUnitId.HasValue).Select(x => (Guid?)x.DowntimeReasonId).FirstOrDefaultAsync(ct);
    public Task<Guid?> FindUnknownReasonAsync(CancellationToken ct) => Db.DowntimeReasons.Where(x => x.Code == "DR-UNKNOWN").Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
    public Task<bool> HasFaultMappingAsync(Guid unitId, string faultCode, CancellationToken ct) => Db.PlcFaultCodeMappings.AnyAsync(x => x.IsActive && x.FaultCode == faultCode && (x.WorkUnitId == unitId || x.WorkUnitId == null), ct);
    public Task<ProductionPlanEntity?> FindPlanAtAsync(Guid unitId, DateTimeOffset producedAt, CancellationToken ct) => Db.CurrentProductionPlans().Where(x => x.WorkUnitId == unitId && x.PlannedStart < producedAt && x.PlannedEnd >= producedAt).OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
    public void AddCheckpoint(CounterCheckpointEntity checkpoint) => Db.CounterCheckpoints.Add(checkpoint);
    public void AddRuntime(PlcDeviceRuntimeStateEntity runtime) => Db.PlcDeviceRuntimeStates.Add(runtime);
    public void AddOutbox(OutboxMessageEntity message) => Db.OutboxMessages.Add(message);
    public void AddUnplannedDelta(ProductionReconciliationItemEntity item) => Db.ProductionReconciliationItems.Add(item);
    public void AddEvent(PlcIntegrationEventEntity item) => Db.PlcIntegrationEvents.Add(item);
}
