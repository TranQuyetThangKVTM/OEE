using Oee.Domain.Entities.Production;

namespace Oee.Application.Operations;

public interface ISampleProductionPlanStore
{
    Task<SampleProductionPlanEntity?> FindAsync(
        Guid id,
        bool tracking,
        CancellationToken ct);

    Task<SampleProductionPlanItemEntity?> FindItemAsync(Guid id, CancellationToken ct);

    Task<SampleProductionPlanItemEntity[]> ReadItemsAsync(
        Guid parentId,
        bool tracking,
        CancellationToken ct);

    Task<SampleProductionPlanItemEntity[]> ReadItemsByIdsAsync(Guid[] ids, CancellationToken ct);

    Task<SamplePlanItemKey[]> ReadExistingKeysAsync(
        Guid parentId,
        Guid productId,
        Guid[] unitIds,
        Guid[] shiftIds,
        CancellationToken ct);

    Task<SamplePlanLookup[]> ReadActiveUnitsAsync(Guid[] ids, CancellationToken ct);

    Task<SamplePlanLookup[]> ReadActiveShiftsAsync(Guid[] ids, CancellationToken ct);

    Task<bool> IsActiveUnitAsync(Guid id, CancellationToken ct);

    Task<bool> IsActiveProductAsync(Guid id, CancellationToken ct);

    Task<bool> IsActiveShiftAsync(Guid id, CancellationToken ct);

    void Add(SampleProductionPlanEntity item);

    void AddItem(SampleProductionPlanItemEntity item);

    void AddItems(IEnumerable<SampleProductionPlanItemEntity> items);
}
