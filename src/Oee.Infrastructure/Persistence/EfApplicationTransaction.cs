using Microsoft.EntityFrameworkCore.Storage;
using Oee.Application.Common;

namespace Oee.Infrastructure.Persistence;

internal sealed class EfApplicationTransaction(IDbContextTransaction transaction, OeeDbContext db) : IApplicationTransaction
{
    private bool completed;
    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        await transaction.CommitAsync(cancellationToken);
        completed = true;
    }
    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        await transaction.RollbackAsync(cancellationToken);
        db.ChangeTracker.Clear();
        completed = true;
    }
    public async ValueTask DisposeAsync()
    {
        try { await transaction.DisposeAsync(); }
        finally { if (!completed) db.ChangeTracker.Clear(); }
    }
}
