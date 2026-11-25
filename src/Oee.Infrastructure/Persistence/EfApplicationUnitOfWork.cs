using System.Data;
using Microsoft.EntityFrameworkCore;
using Oee.Application.Common;
using Oee.Domain.Common;
using Oee.Domain.Platform;

namespace Oee.Infrastructure.Persistence;

/// <summary>Một DbContext cho mọi store tham gia cùng use case/transaction.</summary>
public sealed class EfApplicationUnitOfWork(OeeDbContext db) : IApplicationUnitOfWork
{
    internal OeeDbContext Db => db;
    public bool HasChanges => db.ChangeTracker.HasChanges();

    public async Task<IApplicationTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken) =>
        new EfApplicationTransaction(await db.Database.BeginTransactionAsync(isolationLevel, cancellationToken), db);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        try { return await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException exception) { throw new ConcurrencyConflictException("Data has changed. Reload and try again.", exception); }
        catch (DbUpdateException exception) { throw new PersistenceWriteException("Data could not be saved.", exception); }
    }

    public void ExpectRowVersion(SoftDeletableAuditedEntity entity, byte[] expected) => db.Entry(entity).Property(x => x.RowVersion).OriginalValue = expected;
    public void ExpectUpdatedAt(AuditedEntity entity, DateTimeOffset? expected) => db.Entry(entity).Property(x => x.UpdatedAt).OriginalValue = expected;
    public void RequireUpdatedAtWrite(AuditedEntity entity) => db.Entry(entity).Property(x => x.UpdatedAt).IsModified = true;
    public void AddAudit(AuditLogEntity entry) => db.AuditLogs.Add(entry);
    public void DiscardChanges() => db.ChangeTracker.Clear();
}
