using Microsoft.EntityFrameworkCore;
using Oee.Application.PlcIntegration;
using Oee.Domain.Common;
using Oee.Domain.Entities.Oee;
using Oee.Domain.PlcIntegration;
using Oee.Infrastructure.Persistence;

namespace Oee.Infrastructure.PlcIntegration;

public sealed class EfOpcUaWriteBackStore(EfApplicationUnitOfWork unitOfWork) : IOpcUaWriteBackStore
{
    private OeeDbContext Db => unitOfWork.Db;
    public Task<PlcDeviceMappingEntity[]> ReadDevicesAsync(CancellationToken ct) => Db.PlcDeviceMappings.AsNoTracking().Include(x => x.OpcUaConnection).Where(x => x.IsActive).ToArrayAsync(ct);
    public Task<PlcDeviceRuntimeStateEntity?> FindRuntimeAsync(Guid deviceId, CancellationToken ct) => Db.PlcDeviceRuntimeStates.AsNoTracking().SingleOrDefaultAsync(x => x.PlcDeviceMappingId == deviceId, ct);
    public Task<OpcUaTagMappingEntity[]> ReadWriteTagsAsync(Guid deviceId, CancellationToken ct) => Db.OpcUaTagMappings.AsNoTracking().Where(x => x.PlcDeviceMappingId == deviceId && x.Direction == TagDirectionEnum.Write).ToArrayAsync(ct);
    public Task<OeeSnapshotEntity?> FindLatestSnapshotAsync(Guid unitId, CancellationToken ct) => Db.OeeSnapshots.AsNoTracking().Where(x => x.WorkUnitId == unitId).OrderByDescending(x => x.CalculatedAt).FirstOrDefaultAsync(ct);
    public Task<decimal?> ReadCurrentTargetAsync(Guid unitId, DateTimeOffset now, CancellationToken ct) => Db.CurrentProductionPlans().AsNoTracking().Where(x => x.WorkUnitId == unitId && x.PlannedStart <= now && x.PlannedEnd >= now).OrderByDescending(x => x.Version).Select(x => (decimal?)x.PlannedQuantity).FirstOrDefaultAsync(ct);
    public Task<DateTimeOffset?> ReadOpenDowntimeStartAsync(Guid unitId, CancellationToken ct) => Db.DowntimeEvents.AsNoTracking().Where(x => x.WorkUnitId == unitId && x.EndAt == null).Select(x => (DateTimeOffset?)x.StartAt).SingleOrDefaultAsync(ct);
}
