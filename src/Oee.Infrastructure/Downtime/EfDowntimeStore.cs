using Microsoft.EntityFrameworkCore;
using Oee.Application.Operations;
using Oee.Domain.Downtime;
using Oee.Domain.Entities.Organization;
using Oee.Infrastructure.Persistence;

namespace Oee.Infrastructure.Operations;

public sealed class EfDowntimeStore(EfApplicationUnitOfWork unitOfWork) : IDowntimeStore
{
    private OeeDbContext Db => unitOfWork.Db;
    public Task<DowntimeEventEntity?> FindAsync(Guid id, CancellationToken ct) => Db.DowntimeEvents.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<DowntimeEventEntity[]> ReadAsync(Guid[] ids, CancellationToken ct) => Db.DowntimeEvents.Where(x => ids.Contains(x.Id)).ToArrayAsync(ct);
    public Task<DowntimeReasonEntity?> FindReasonAsync(Guid id, CancellationToken ct) => Db.DowntimeReasons.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> HasOpenAsync(Guid unitId, CancellationToken ct) => Db.DowntimeEvents.AnyAsync(x => x.WorkUnitId == unitId && x.EndAt == null, ct);
    public Task<bool> HasOverlapAsync(Guid unitId, DateTimeOffset start, DateTimeOffset endAt, CancellationToken ct) => Db.DowntimeEvents.AnyAsync(x => x.WorkUnitId == unitId && x.StartAt < endAt && (x.EndAt == null || x.EndAt > start), ct);
    public void Add(DowntimeEventEntity item) => Db.DowntimeEvents.Add(item);
}
