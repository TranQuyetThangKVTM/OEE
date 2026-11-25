using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Oee.Application.Oee;
using Oee.Domain.Common;
using Oee.Domain.Downtime;
using Oee.Domain.Entities.Oee;
using Oee.Domain.Entities.Organization;
using Oee.Domain.Entities.Production;
using Oee.Infrastructure.Persistence;

namespace Oee.Infrastructure.Operations;

public sealed class EfOeeStore(EfApplicationUnitOfWork unitOfWork) : IOeeSnapshotStore, IOeeAlertStore, IOeeTargetStore
{
    private OeeDbContext Db => unitOfWork.Db;
    public Task<ProductionOutputEntity[]> ReadOutputsAsync(Guid unitId, DateTimeOffset from, DateTimeOffset until, CancellationToken ct) => Db.ProductionOutputs.AsNoTracking().Where(x => x.WorkUnitId == unitId && x.PeriodEnd > from && x.PeriodEnd <= until).ToArrayAsync(ct);
    public Task<DowntimeEventEntity[]> ReadDowntimesAsync(Guid unitId, DateTimeOffset from, DateTimeOffset until, CancellationToken ct) => Db.DowntimeEvents.AsNoTracking().Where(x => x.WorkUnitId == unitId && x.StartAt < until && (x.EndAt == null || x.EndAt > from)).ToArrayAsync(ct);
    public Task<OeeSnapshotEntity?> FindSnapshotAsync(Guid unitId, OeePeriodTypeEnum periodType, string periodCode, DateTimeOffset periodStart, string calculationVersion, CancellationToken ct) => Db.OeeSnapshots.SingleOrDefaultAsync(x => x.WorkUnitId == unitId && x.PeriodType == periodType && x.PeriodCode == periodCode && x.PeriodStart == periodStart && x.CalculationVersion == calculationVersion, ct);
    public Task<Guid[]> ReadActiveUnitIdsAsync(DateTimeOffset from, DateTimeOffset until, CancellationToken ct) => Db.ProductionOutputs.AsNoTracking().Where(x => x.PeriodEnd >= from && x.PeriodEnd <= until).Select(x => x.WorkUnitId).Distinct().ToArrayAsync(ct);
    public Task<OeeWorkUnitScope[]> ReadScopesAsync(Guid[] unitIds, CancellationToken ct) => Db.WorkUnits.AsNoTracking().Where(x => unitIds.Contains(x.Id)).Select(x => new OeeWorkUnitScope(x.Id, x.WorkCenter.Area.SiteId, x.WorkCenter.Area.Site.TimeZoneId)).ToArrayAsync(ct);
    public Task<OeeWorkUnitScope> ReadScopeAsync(Guid unitId, CancellationToken ct) => Db.WorkUnits.AsNoTracking().Where(x => x.Id == unitId).Select(x => new OeeWorkUnitScope(x.Id, x.WorkCenter.Area.SiteId, x.WorkCenter.Area.Site.TimeZoneId)).SingleAsync(ct);
    public Task<ShiftEntity[]> ReadShiftsAsync(Guid siteId, CancellationToken ct) => Db.Shifts.AsNoTracking().Where(x => x.SiteId == siteId).ToArrayAsync(ct);
    public Task<OeeTargetEntity[]> ReadTargetsAsync(Guid unitId, Guid siteId, OeePeriodTypeEnum periodType, DateOnly effectiveOn, CancellationToken ct) => Db.OeeTargets.AsNoTracking().Where(x => x.PeriodType == periodType && x.EffectiveFrom <= effectiveOn && (!x.EffectiveTo.HasValue || x.EffectiveTo >= effectiveOn) && (x.WorkUnitId == unitId || (x.WorkUnitId == null && (x.SiteId == siteId || x.SiteId == null)))).ToArrayAsync(ct);
    public void Add(OeeSnapshotEntity snapshot) => Db.OeeSnapshots.Add(snapshot);
    Task<OeeAlertEntity?> IOeeAlertStore.FindAsync(Guid id, CancellationToken ct) => Db.OeeAlerts.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<OeeAlertEntity?> FindWithSnapshotAsync(Guid id, CancellationToken ct) => Db.OeeAlerts.AsNoTracking().Include(x => x.Snapshot).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<OeeAlertEntity?> FindUnresolvedAsync(Guid snapshotId, AlertMetricEnum metric, CancellationToken ct) => Db.OeeAlerts.SingleOrDefaultAsync(x => x.SnapshotId == snapshotId && x.Metric == metric && x.State != AlertStateEnum.Resolved, ct);
    public Task<bool> HasTargetHistoryAsync(Guid targetId, CancellationToken ct) => Db.OeeAlerts.AnyAsync(x => x.TargetId == targetId, ct);
    public void Add(OeeAlertEntity alert) => Db.OeeAlerts.Add(alert);
    Task<OeeTargetEntity?> IOeeTargetStore.FindAsync(Guid id, CancellationToken ct) => Db.OeeTargets.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<OeeTargetEntity?> FindPreviousAsync(Expression<Func<OeeTargetEntity, bool>> predicate, CancellationToken ct) => Db.OeeTargets.Where(predicate).OrderByDescending(x => x.EffectiveFrom).FirstOrDefaultAsync(ct);
    public Task<bool> AnyAsync(Expression<Func<OeeTargetEntity, bool>> predicate, CancellationToken ct) => Db.OeeTargets.AnyAsync(predicate, ct);
    public void Add(OeeTargetEntity target) => Db.OeeTargets.Add(target);
}
