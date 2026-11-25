using Oee.Domain.Downtime;
using Oee.Domain.Entities.Organization;

namespace Oee.Application.Operations;

public interface IDowntimeStore
{
    Task<DowntimeEventEntity?> FindAsync(Guid id, CancellationToken ct);

    Task<DowntimeEventEntity[]> ReadAsync(Guid[] ids, CancellationToken ct);

    Task<DowntimeReasonEntity?> FindReasonAsync(Guid id, CancellationToken ct);

    Task<bool> HasOpenAsync(Guid unitId, CancellationToken ct);

    Task<bool> HasOverlapAsync(
        Guid unitId,
        DateTimeOffset start,
        DateTimeOffset endAt,
        CancellationToken ct);

    void Add(DowntimeEventEntity item);
}
