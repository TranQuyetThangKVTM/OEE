using Oee.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Oee.Application.Operations;
using Oee.Domain.Entities.Organization;
using Oee.Domain.Entities.Production;
using Oee.Infrastructure.Persistence;

namespace Oee.Infrastructure.Operations;

public sealed class EfProductionPlanStore(EfApplicationUnitOfWork unitOfWork) : IProductionPlanStore
{
    private OeeDbContext Db => unitOfWork.Db;
    public Task<ProductionPlanEntity?> FindCurrentAsync(Guid id, bool tracking, CancellationToken ct) =>
        (tracking ? Db.CurrentProductionPlans() : Db.CurrentProductionPlans().AsNoTracking()).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> HasOutputAsync(Guid planId, CancellationToken ct) => Db.ProductionOutputs.AnyAsync(x => x.ProductionPlanId == planId, ct);
    public async Task<(ProductionPlanEntity[] Items, int Total)> ReadCurrentPageAsync(DateTimeOffset? from, DateTimeOffset? until, int page, int pageSize, CancellationToken ct)
    {
        var query = Db.CurrentProductionPlans().AsNoTracking();
        if (from.HasValue) query = query.Where(x => x.PlannedEnd >= from.Value);
        if (until.HasValue) query = query.Where(x => x.PlannedStart <= until.Value);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.PlannedStart).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        return (items, total);
    }
    public Task<Guid[]> ReadPlanIdsWithOutputAsync(Guid[] ids, CancellationToken ct) => Db.ProductionOutputs.AsNoTracking().Where(x => ids.Contains(x.ProductionPlanId)).Select(x => x.ProductionPlanId).Distinct().ToArrayAsync(ct);
    public Task<string[]> ReadReservedCodesAsync(string prefix, CancellationToken ct) => Db.ProductionPlans.AsNoTracking()
        .Where(x => x.Code.StartsWith(prefix)).Select(x => x.Code).ToArrayAsync(ct);
    public Task<WorkUnitEntity[]> ReadActiveUnitsAsync(Guid[] ids, CancellationToken ct) => Db.WorkUnits.AsNoTracking()
        .Include(x => x.WorkCenter).ThenInclude(x => x.Area).ThenInclude(x => x.Site)
        .Where(x => ids.Contains(x.Id) && x.Status == RecordStatusEnum.Active && x.WorkCenter.Status == RecordStatusEnum.Active && x.WorkCenter.Area.Status == RecordStatusEnum.Active && x.WorkCenter.Area.Site.Status == RecordStatusEnum.Active).ToArrayAsync(ct);
    public Task<ShiftEntity[]> ReadActiveShiftsAsync(Guid[] ids, CancellationToken ct) => Db.Shifts.AsNoTracking().Include(x => x.Site).Where(x => ids.Contains(x.Id) && x.Status == RecordStatusEnum.Active && x.Site.Status == RecordStatusEnum.Active).ToArrayAsync(ct);
    public Task<ProductEntity?> FindActiveProductAsync(Guid id, CancellationToken ct) => Db.Products.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.Status == RecordStatusEnum.Active, ct);
    public Task<SampleProductionPlanEntity?> FindSampleAsync(Guid id, CancellationToken ct) => Db.SampleProductionPlans.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<SampleProductionPlanItemEntity[]> ReadSampleItemsAsync(Guid id, CancellationToken ct) => Db.SampleProductionPlanItems.AsNoTracking()
        .Include(x => x.WorkUnit).ThenInclude(x => x.WorkCenter).ThenInclude(x => x.Area).ThenInclude(x => x.Site)
        .Include(x => x.Shift).ThenInclude(x => x.Site).Include(x => x.Product).Where(x => x.SampleProductionPlanId == id).ToArrayAsync(ct);
    public Task<ProductionPlanEntity[]> ReadOverlappingPlansAsync(Guid[] unitIds, DateTimeOffset from, DateTimeOffset until, CancellationToken ct) => Db.CurrentProductionPlans().AsNoTracking()
        .Where(x => unitIds.Contains(x.WorkUnitId) && x.PlannedStart < until && x.PlannedEnd > from).ToArrayAsync(ct);
    public Task<Guid?> ReadActiveUnitSiteAsync(Guid id, CancellationToken ct) => Db.WorkUnits.Where(x => x.Id == id && x.Status == RecordStatusEnum.Active && x.WorkCenter.Status == RecordStatusEnum.Active && x.WorkCenter.Area.Status == RecordStatusEnum.Active && x.WorkCenter.Area.Site.Status == RecordStatusEnum.Active).Select(x => (Guid?)x.WorkCenter.Area.SiteId).SingleOrDefaultAsync(ct);
    public Task<bool> IsActiveProductAsync(Guid id, CancellationToken ct) => Db.Products.AnyAsync(x => x.Id == id && x.Status == RecordStatusEnum.Active, ct);
    public Task<bool> IsActiveShiftForSiteAsync(Guid id, Guid siteId, CancellationToken ct) => Db.Shifts.AnyAsync(x => x.Id == id && x.Status == RecordStatusEnum.Active && x.SiteId == siteId, ct);
    public Task<bool> HasOverlappingPlanAsync(Guid unitId, Guid? excludedId, DateTimeOffset start, DateTimeOffset endAt, CancellationToken ct) => Db.CurrentProductionPlans().AnyAsync(x => x.Id != excludedId && x.WorkUnitId == unitId && x.PlannedStart < endAt && x.PlannedEnd > start, ct);
    public void Add(ProductionPlanEntity plan) => Db.ProductionPlans.Add(plan);
    public void AddRange(IEnumerable<ProductionPlanEntity> plans) => Db.ProductionPlans.AddRange(plans);
}
