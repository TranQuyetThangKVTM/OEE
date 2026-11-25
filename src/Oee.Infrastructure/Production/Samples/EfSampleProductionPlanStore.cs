using Oee.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Oee.Application.Operations;
using Oee.Domain.Entities.Production;
using Oee.Infrastructure.Persistence;

namespace Oee.Infrastructure.Operations;

public sealed class EfSampleProductionPlanStore(EfApplicationUnitOfWork unitOfWork) : ISampleProductionPlanStore
{
    private OeeDbContext Db => unitOfWork.Db;
    public Task<SampleProductionPlanEntity?> FindAsync(Guid id, bool tracking, CancellationToken ct) => (tracking ? Db.SampleProductionPlans : Db.SampleProductionPlans.AsNoTracking()).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<SampleProductionPlanItemEntity?> FindItemAsync(Guid id, CancellationToken ct) => Db.SampleProductionPlanItems.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<SampleProductionPlanItemEntity[]> ReadItemsAsync(Guid parentId, bool tracking, CancellationToken ct) => (tracking ? Db.SampleProductionPlanItems : Db.SampleProductionPlanItems.AsNoTracking()).Where(x => x.SampleProductionPlanId == parentId).ToArrayAsync(ct);
    public Task<SampleProductionPlanItemEntity[]> ReadItemsByIdsAsync(Guid[] ids, CancellationToken ct) => Db.SampleProductionPlanItems.Where(x => ids.Contains(x.Id)).ToArrayAsync(ct);
    public Task<SamplePlanItemKey[]> ReadExistingKeysAsync(Guid parentId, Guid productId, Guid[] unitIds, Guid[] shiftIds, CancellationToken ct) => Db.SampleProductionPlanItems.AsNoTracking().Where(x => x.SampleProductionPlanId == parentId && x.ProductId == productId && unitIds.Contains(x.WorkUnitId) && shiftIds.Contains(x.ShiftId)).Select(x => new SamplePlanItemKey(x.WorkUnitId, x.ShiftId)).ToArrayAsync(ct);
    public Task<SamplePlanLookup[]> ReadActiveUnitsAsync(Guid[] ids, CancellationToken ct) => Db.WorkUnits.AsNoTracking().Where(x => ids.Contains(x.Id) && x.Status == RecordStatusEnum.Active && x.WorkCenter.Status == RecordStatusEnum.Active && x.WorkCenter.Area.Status == RecordStatusEnum.Active && x.WorkCenter.Area.Site.Status == RecordStatusEnum.Active).Select(x => new SamplePlanLookup(x.Id, x.Code, x.WorkCenter.Area.SiteId)).ToArrayAsync(ct);
    public Task<SamplePlanLookup[]> ReadActiveShiftsAsync(Guid[] ids, CancellationToken ct) => Db.Shifts.AsNoTracking().Where(x => ids.Contains(x.Id) && x.Status == RecordStatusEnum.Active && x.Site.Status == RecordStatusEnum.Active).Select(x => new SamplePlanLookup(x.Id, x.Code, x.SiteId)).ToArrayAsync(ct);
    public Task<bool> IsActiveUnitAsync(Guid id, CancellationToken ct) => Db.WorkUnits.AnyAsync(x => x.Id == id && x.Status == RecordStatusEnum.Active, ct);
    public Task<bool> IsActiveProductAsync(Guid id, CancellationToken ct) => Db.Products.AnyAsync(x => x.Id == id && x.Status == RecordStatusEnum.Active, ct);
    public Task<bool> IsActiveShiftAsync(Guid id, CancellationToken ct) => Db.Shifts.AnyAsync(x => x.Id == id && x.Status == RecordStatusEnum.Active, ct);
    public void Add(SampleProductionPlanEntity item) => Db.SampleProductionPlans.Add(item);
    public void AddItem(SampleProductionPlanItemEntity item) => Db.SampleProductionPlanItems.Add(item);
    public void AddItems(IEnumerable<SampleProductionPlanItemEntity> items) => Db.SampleProductionPlanItems.AddRange(items);
}
