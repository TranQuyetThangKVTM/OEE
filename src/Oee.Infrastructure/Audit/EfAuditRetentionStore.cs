using Microsoft.EntityFrameworkCore;
using Oee.Application.Audit;
using Oee.Domain.Platform;
using Oee.Infrastructure.Persistence;

namespace Oee.Infrastructure.Audit;

public sealed class EfAuditRetentionStore(EfApplicationUnitOfWork unitOfWork) : IAuditRetentionStore
{
    private OeeDbContext Db => unitOfWork.Db;
    public Task<int> CountBeforeAsync(DateTimeOffset cutoff, CancellationToken ct) => Db.AuditLogs.AsNoTracking().CountAsync(log => log.Timestamp < cutoff, ct);
    public Task<Guid[]> ReadBatchIdsAsync(DateTimeOffset cutoff, int batchSize, CancellationToken ct) => Db.AuditLogs.AsNoTracking().Where(log => log.Timestamp < cutoff).OrderBy(log => log.Timestamp).Select(log => log.Id).Take(batchSize).ToArrayAsync(ct);
    public Task<int> DeleteIdsAsync(Guid[] ids, CancellationToken ct) => Db.AuditLogs.Where(log => ids.Contains(log.Id)).ExecuteDeleteAsync(ct);
    public Task<int> DeleteBeforeAsync(DateTimeOffset cutoff, CancellationToken ct) => Db.AuditLogs.Where(log => log.Timestamp < cutoff).ExecuteDeleteAsync(ct);
    public void AddRun(AuditRetentionRunEntity run) => Db.AuditRetentionRuns.Add(run);
}
