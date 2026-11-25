using System.Linq.Expressions;
using Oee.Domain.Downtime;
using Oee.Domain.Entities.Oee;
using Oee.Domain.Entities.Organization;
using Oee.Domain.Entities.Production;
using Oee.Domain.PlcIntegration;

namespace Oee.Application.Operations;

/// <summary>Truy xuất master và kiểm tra tham chiếu theo tiêu chí nghiệp vụ; không trả IQueryable hoặc API EF.</summary>
public interface IMasterDataStore
{
    Task<bool> AnyWorkCentersAsync(
        Expression<Func<WorkCenterEntity, bool>> predicate,
        CancellationToken ct);

    Task<bool> AnyDowntimeEventsAsync(
        Expression<Func<DowntimeEventEntity, bool>> predicate,
        CancellationToken ct);

    Task<bool> AnyPlcFaultCodeMappingsAsync(
        Expression<Func<PlcFaultCodeMappingEntity, bool>> predicate,
        CancellationToken ct);

    Task<bool> AnySitesAsync(Expression<Func<SiteEntity, bool>> predicate, CancellationToken ct);

    Task<bool> AnyWorkUnitStandardsAsync(
        Expression<Func<WorkUnitStandardEntity, bool>> predicate,
        CancellationToken ct);

    Task<bool> AnyProductionPlansAsync(
        Expression<Func<ProductionPlanEntity, bool>> predicate,
        CancellationToken ct);

    Task<bool> AnyProductionOutputsAsync(
        Expression<Func<ProductionOutputEntity, bool>> predicate,
        CancellationToken ct);

    Task<bool> AnyProductionReconciliationItemsAsync(
        Expression<Func<ProductionReconciliationItemEntity, bool>> predicate,
        CancellationToken ct);

    Task<bool> AnySampleProductionPlanItemsAsync(
        Expression<Func<SampleProductionPlanItemEntity, bool>> predicate,
        CancellationToken ct);

    Task<bool> AnyPlcDeviceMappingsAsync(
        Expression<Func<PlcDeviceMappingEntity, bool>> predicate,
        CancellationToken ct);

    Task<bool> AnyAreasAsync(Expression<Func<AreaEntity, bool>> predicate, CancellationToken ct);

    Task<bool> AnyShiftsAsync(Expression<Func<ShiftEntity, bool>> predicate, CancellationToken ct);

    Task<bool> AnyOeeTargetsAsync(
        Expression<Func<OeeTargetEntity, bool>> predicate,
        CancellationToken ct);

    Task<bool> AnyWorkUnitsAsync(
        Expression<Func<WorkUnitEntity, bool>> predicate,
        CancellationToken ct);

    Task<bool> AnyOeeSnapshotsAsync(
        Expression<Func<OeeSnapshotEntity, bool>> predicate,
        CancellationToken ct);

    Task<bool> AnyEnterprisesAsync(
        Expression<Func<EnterpriseEntity, bool>> predicate,
        CancellationToken ct);

    Task<bool> AnyProductsAsync(Expression<Func<ProductEntity, bool>> predicate, CancellationToken ct);

    Task<bool> AnyDowntimeReasonsAsync(
        Expression<Func<DowntimeReasonEntity, bool>> predicate,
        CancellationToken ct);

    Task<EnterpriseEntity?> FindEnterpriseAsync(Guid id, CancellationToken ct);

    void AddEnterprise(EnterpriseEntity entity);

    Task<SiteEntity?> FindSiteAsync(Guid id, CancellationToken ct);

    void AddSite(SiteEntity entity);

    Task<AreaEntity?> FindAreaAsync(Guid id, CancellationToken ct);

    void AddArea(AreaEntity entity);

    Task<WorkCenterEntity?> FindWorkCenterAsync(Guid id, CancellationToken ct);

    void AddWorkCenter(WorkCenterEntity entity);

    Task<WorkUnitEntity?> FindWorkUnitAsync(Guid id, CancellationToken ct);

    void AddWorkUnit(WorkUnitEntity entity);

    Task<ProductEntity?> FindProductAsync(Guid id, CancellationToken ct);

    void AddProduct(ProductEntity entity);

    Task<WorkUnitStandardEntity?> FindWorkUnitStandardAsync(Guid id, CancellationToken ct);

    void AddWorkUnitStandard(WorkUnitStandardEntity entity);

    Task<ShiftEntity?> FindShiftAsync(Guid id, CancellationToken ct);

    void AddShift(ShiftEntity entity);

    Task<DowntimeReasonEntity?> FindDowntimeReasonAsync(Guid id, CancellationToken ct);

    void AddDowntimeReason(DowntimeReasonEntity entity);

    Task<bool> AnyCurrentProductionPlansAsync(
        Expression<Func<ProductionPlanEntity, bool>> predicate,
        CancellationToken ct);

    Task<MasterShiftInterval[]> ReadShiftIntervalsAsync(
        Guid siteId,
        Guid excludedId,
        CancellationToken ct);

    Task<WorkUnitStandardEntity?> FindPreviousStandardAsync(
        Guid unitId,
        Guid productId,
        DateOnly effectiveFrom,
        CancellationToken ct);
}
