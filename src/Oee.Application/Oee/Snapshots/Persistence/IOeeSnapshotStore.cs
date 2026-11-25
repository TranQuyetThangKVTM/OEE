using Oee.Domain.Common;
using Oee.Domain.Downtime;
using Oee.Domain.Entities.Oee;
using Oee.Domain.Entities.Organization;
using Oee.Domain.Entities.Production;

namespace Oee.Application.Oee;

public interface IOeeSnapshotStore
{
    Task<ProductionOutputEntity[]> ReadOutputsAsync(
        Guid unitId,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken ct);

    Task<DowntimeEventEntity[]> ReadDowntimesAsync(
        Guid unitId,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken ct);

    Task<OeeSnapshotEntity?> FindSnapshotAsync(
        Guid unitId,
        OeePeriodTypeEnum periodType,
        string periodCode,
        DateTimeOffset periodStart,
        string calculationVersion,
        CancellationToken ct);

    Task<Guid[]> ReadActiveUnitIdsAsync(
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken ct);

    Task<OeeWorkUnitScope[]> ReadScopesAsync(Guid[] unitIds, CancellationToken ct);

    Task<OeeWorkUnitScope> ReadScopeAsync(Guid unitId, CancellationToken ct);

    Task<ShiftEntity[]> ReadShiftsAsync(Guid siteId, CancellationToken ct);

    Task<OeeTargetEntity[]> ReadTargetsAsync(
        Guid unitId,
        Guid siteId,
        OeePeriodTypeEnum periodType,
        DateOnly effectiveOn,
        CancellationToken ct);

    void Add(OeeSnapshotEntity snapshot);
}
