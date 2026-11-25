using Oee.Domain.Common;
using Oee.Domain.Entities.Oee;

namespace Oee.Application.Oee;

public interface IOeeAlertStore
{
    Task<OeeAlertEntity?> FindAsync(Guid id, CancellationToken ct);

    Task<OeeAlertEntity?> FindWithSnapshotAsync(Guid id, CancellationToken ct);

    Task<OeeAlertEntity?> FindUnresolvedAsync(
        Guid snapshotId,
        AlertMetricEnum metric,
        CancellationToken ct);

    Task<bool> HasTargetHistoryAsync(Guid targetId, CancellationToken ct);

    void Add(OeeAlertEntity alert);
}
