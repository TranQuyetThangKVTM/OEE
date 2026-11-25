using Oee.Domain.Common;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Oee.Application.Operations;
using Oee.Domain.Downtime;
using Oee.Domain.Entities.Oee;
using Oee.Domain.Entities.Organization;
using Oee.Domain.Entities.Production;
using Oee.Domain.PlcIntegration;
using Oee.Infrastructure.Persistence;

namespace Oee.Infrastructure.Operations;

public sealed class EfMasterDataStore(EfApplicationUnitOfWork unitOfWork) : IMasterDataStore
{
    private OeeDbContext Db => unitOfWork.Db;
    public Task<bool> AnyWorkCentersAsync(Expression<Func<WorkCenterEntity, bool>> predicate, CancellationToken ct) => Db.WorkCenters.AnyAsync(predicate, ct);
    public Task<bool> AnyDowntimeEventsAsync(Expression<Func<DowntimeEventEntity, bool>> predicate, CancellationToken ct) => Db.DowntimeEvents.AnyAsync(predicate, ct);
    public Task<bool> AnyPlcFaultCodeMappingsAsync(Expression<Func<PlcFaultCodeMappingEntity, bool>> predicate, CancellationToken ct) => Db.PlcFaultCodeMappings.AnyAsync(predicate, ct);
    public Task<bool> AnySitesAsync(Expression<Func<SiteEntity, bool>> predicate, CancellationToken ct) => Db.Sites.AnyAsync(predicate, ct);
    public Task<bool> AnyWorkUnitStandardsAsync(Expression<Func<WorkUnitStandardEntity, bool>> predicate, CancellationToken ct) => Db.WorkUnitStandards.AnyAsync(predicate, ct);
    public Task<bool> AnyProductionPlansAsync(Expression<Func<ProductionPlanEntity, bool>> predicate, CancellationToken ct) => Db.ProductionPlans.AnyAsync(predicate, ct);
    public Task<bool> AnyProductionOutputsAsync(Expression<Func<ProductionOutputEntity, bool>> predicate, CancellationToken ct) => Db.ProductionOutputs.AnyAsync(predicate, ct);
    public Task<bool> AnyProductionReconciliationItemsAsync(Expression<Func<ProductionReconciliationItemEntity, bool>> predicate, CancellationToken ct) => Db.ProductionReconciliationItems.AnyAsync(predicate, ct);
    public Task<bool> AnySampleProductionPlanItemsAsync(Expression<Func<SampleProductionPlanItemEntity, bool>> predicate, CancellationToken ct) => Db.SampleProductionPlanItems.AnyAsync(predicate, ct);
    public Task<bool> AnyPlcDeviceMappingsAsync(Expression<Func<PlcDeviceMappingEntity, bool>> predicate, CancellationToken ct) => Db.PlcDeviceMappings.AnyAsync(predicate, ct);
    public Task<bool> AnyAreasAsync(Expression<Func<AreaEntity, bool>> predicate, CancellationToken ct) => Db.Areas.AnyAsync(predicate, ct);
    public Task<bool> AnyShiftsAsync(Expression<Func<ShiftEntity, bool>> predicate, CancellationToken ct) => Db.Shifts.AnyAsync(predicate, ct);
    public Task<bool> AnyOeeTargetsAsync(Expression<Func<OeeTargetEntity, bool>> predicate, CancellationToken ct) => Db.OeeTargets.AnyAsync(predicate, ct);
    public Task<bool> AnyWorkUnitsAsync(Expression<Func<WorkUnitEntity, bool>> predicate, CancellationToken ct) => Db.WorkUnits.AnyAsync(predicate, ct);
    public Task<bool> AnyOeeSnapshotsAsync(Expression<Func<OeeSnapshotEntity, bool>> predicate, CancellationToken ct) => Db.OeeSnapshots.AnyAsync(predicate, ct);
    public Task<bool> AnyEnterprisesAsync(Expression<Func<EnterpriseEntity, bool>> predicate, CancellationToken ct) => Db.Enterprises.AnyAsync(predicate, ct);
    public Task<bool> AnyProductsAsync(Expression<Func<ProductEntity, bool>> predicate, CancellationToken ct) => Db.Products.AnyAsync(predicate, ct);
    public Task<bool> AnyDowntimeReasonsAsync(Expression<Func<DowntimeReasonEntity, bool>> predicate, CancellationToken ct) => Db.DowntimeReasons.AnyAsync(predicate, ct);
    public Task<EnterpriseEntity?> FindEnterpriseAsync(Guid id, CancellationToken ct) => Db.Enterprises.SingleOrDefaultAsync(x => x.Id == id, ct);
    public void AddEnterprise(EnterpriseEntity entity) => Db.Enterprises.Add(entity);
    public Task<SiteEntity?> FindSiteAsync(Guid id, CancellationToken ct) => Db.Sites.SingleOrDefaultAsync(x => x.Id == id, ct);
    public void AddSite(SiteEntity entity) => Db.Sites.Add(entity);
    public Task<AreaEntity?> FindAreaAsync(Guid id, CancellationToken ct) => Db.Areas.SingleOrDefaultAsync(x => x.Id == id, ct);
    public void AddArea(AreaEntity entity) => Db.Areas.Add(entity);
    public Task<WorkCenterEntity?> FindWorkCenterAsync(Guid id, CancellationToken ct) => Db.WorkCenters.SingleOrDefaultAsync(x => x.Id == id, ct);
    public void AddWorkCenter(WorkCenterEntity entity) => Db.WorkCenters.Add(entity);
    public Task<WorkUnitEntity?> FindWorkUnitAsync(Guid id, CancellationToken ct) => Db.WorkUnits.SingleOrDefaultAsync(x => x.Id == id, ct);
    public void AddWorkUnit(WorkUnitEntity entity) => Db.WorkUnits.Add(entity);
    public Task<ProductEntity?> FindProductAsync(Guid id, CancellationToken ct) => Db.Products.SingleOrDefaultAsync(x => x.Id == id, ct);
    public void AddProduct(ProductEntity entity) => Db.Products.Add(entity);
    public Task<WorkUnitStandardEntity?> FindWorkUnitStandardAsync(Guid id, CancellationToken ct) => Db.WorkUnitStandards.SingleOrDefaultAsync(x => x.Id == id, ct);
    public void AddWorkUnitStandard(WorkUnitStandardEntity entity) => Db.WorkUnitStandards.Add(entity);
    public Task<ShiftEntity?> FindShiftAsync(Guid id, CancellationToken ct) => Db.Shifts.SingleOrDefaultAsync(x => x.Id == id, ct);
    public void AddShift(ShiftEntity entity) => Db.Shifts.Add(entity);
    public Task<DowntimeReasonEntity?> FindDowntimeReasonAsync(Guid id, CancellationToken ct) => Db.DowntimeReasons.SingleOrDefaultAsync(x => x.Id == id, ct);
    public void AddDowntimeReason(DowntimeReasonEntity entity) => Db.DowntimeReasons.Add(entity);
    public Task<bool> AnyCurrentProductionPlansAsync(Expression<Func<ProductionPlanEntity, bool>> predicate, CancellationToken ct) => Db.CurrentProductionPlans().AnyAsync(predicate, ct);
    public Task<MasterShiftInterval[]> ReadShiftIntervalsAsync(Guid siteId, Guid excludedId, CancellationToken ct) => Db.Shifts.AsNoTracking().Where(x => x.SiteId == siteId && x.Id != excludedId && x.Status == RecordStatusEnum.Active).Select(x => new MasterShiftInterval(x.StartTime, x.EndTime)).ToArrayAsync(ct);
    public Task<WorkUnitStandardEntity?> FindPreviousStandardAsync(Guid unitId, Guid productId, DateOnly effectiveFrom, CancellationToken ct) => Db.WorkUnitStandards.Where(x => x.WorkUnitId == unitId && x.ProductId == productId && x.EffectiveFrom < effectiveFrom && (!x.EffectiveTo.HasValue || x.EffectiveTo >= effectiveFrom)).OrderByDescending(x => x.EffectiveFrom).FirstOrDefaultAsync(ct);
}
