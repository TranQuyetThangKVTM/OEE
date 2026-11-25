using Oee.Domain.Platform;

namespace Oee.Application.Audit;

public interface IAuditRetentionStore
{
    Task<int> CountBeforeAsync(DateTimeOffset cutoff, CancellationToken ct);

    Task<Guid[]> ReadBatchIdsAsync(
        DateTimeOffset cutoff,
        int batchSize,
        CancellationToken ct);

    Task<int> DeleteIdsAsync(Guid[] ids, CancellationToken ct);

    Task<int> DeleteBeforeAsync(DateTimeOffset cutoff, CancellationToken ct);

    void AddRun(AuditRetentionRunEntity run);
}
