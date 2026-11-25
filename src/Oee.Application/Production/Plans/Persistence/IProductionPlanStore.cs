using Oee.Domain.Entities.Organization;
using Oee.Domain.Entities.Production;

namespace Oee.Application.Operations;

/// <summary>Các phép truy xuất dữ liệu cho use case kế hoạch; điều kiện nghiệp vụ nằm ở service Application.</summary>
public interface IProductionPlanStore
{
    Task<ProductionPlanEntity?> FindCurrentAsync(
        Guid id,
        bool tracking,
        CancellationToken ct);

    Task<bool> HasOutputAsync(Guid planId, CancellationToken ct);

    Task<(ProductionPlanEntity[] Items, int Total)> ReadCurrentPageAsync(
        DateTimeOffset? from,
        DateTimeOffset? until,
        int page,
        int pageSize,
        CancellationToken ct);

    Task<Guid[]> ReadPlanIdsWithOutputAsync(Guid[] ids, CancellationToken ct);

    Task<string[]> ReadReservedCodesAsync(string prefix, CancellationToken ct);

    Task<WorkUnitEntity[]> ReadActiveUnitsAsync(Guid[] ids, CancellationToken ct);

    Task<ShiftEntity[]> ReadActiveShiftsAsync(Guid[] ids, CancellationToken ct);

    Task<ProductEntity?> FindActiveProductAsync(Guid id, CancellationToken ct);

    Task<SampleProductionPlanEntity?> FindSampleAsync(Guid id, CancellationToken ct);

    Task<SampleProductionPlanItemEntity[]> ReadSampleItemsAsync(Guid id, CancellationToken ct);

    Task<ProductionPlanEntity[]> ReadOverlappingPlansAsync(
        Guid[] unitIds,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken ct);

    Task<Guid?> ReadActiveUnitSiteAsync(Guid id, CancellationToken ct);

    Task<bool> IsActiveProductAsync(Guid id, CancellationToken ct);

    Task<bool> IsActiveShiftForSiteAsync(
        Guid id,
        Guid siteId,
        CancellationToken ct);

    Task<bool> HasOverlappingPlanAsync(
        Guid unitId,
        Guid? excludedId,
        DateTimeOffset start,
        DateTimeOffset endAt,
        CancellationToken ct);

    void Add(ProductionPlanEntity plan);

    void AddRange(IEnumerable<ProductionPlanEntity> plans);
}
