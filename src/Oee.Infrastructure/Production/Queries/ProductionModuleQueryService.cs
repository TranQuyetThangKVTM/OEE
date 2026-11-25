using Microsoft.EntityFrameworkCore;
using Oee.Application.Common;
using Oee.Application.Contracts.Modules;
using Oee.Application.Modules;
using Oee.Domain.Common;
using Oee.Domain.Downtime;
using Oee.Domain.Entities.Production;
using Oee.Infrastructure.Persistence;

namespace Oee.Infrastructure.Operations;

/// <summary>
/// Query service cho danh sách kế hoạch, sản lượng, downtime và kế hoạch mẫu.
/// </summary>
/// <remarks>
/// Filter/sort/paging được giữ dưới dạng IQueryable đến khi ToArrayAsync để EF dịch xuống SQL.
/// Projection sang DTO ngăn UI nhận Entity/navigation; các batch query phụ theo tập Id tránh N+1.
/// OEE của output được tính bằng Domain engine dùng chung, không viết công thức trong Razor.
/// </remarks>
public sealed class ProductionModuleQueryService(OeeDbContext db, IClock clock) : IProductionModuleQueryService
{
    /// <summary>Đọc lookup active và timezone Site cho form Production.</summary>
    public async Task<ProductionLookups> LookupsAsync(CancellationToken ct)
    {
        var workUnits = await db.WorkUnits.AsNoTracking().Where(x => x.Status == RecordStatusEnum.Active).OrderBy(x => x.Code)
            .Select(x => new ProductionLookupOption(x.Id, x.Code, x.Name, x.WorkCenterId,
                x.WorkCenter.Area.Site.TimeZoneId, null, null, x.WorkCenter.Area.SiteId)).ToArrayAsync(ct);
        var workCenters = await db.WorkCenters.AsNoTracking().Where(x => x.Status == RecordStatusEnum.Active).OrderBy(x => x.Code)
            .Select(x => new ProductionLookupOption(x.Id, x.Code, x.Name, x.AreaId)).ToArrayAsync(ct);
        var products = await db.Products.AsNoTracking().Where(x => x.Status == RecordStatusEnum.Active).OrderBy(x => x.Code)
            .Select(x => new ProductionLookupOption(x.Id, x.Code, x.Name)).ToArrayAsync(ct);
        var shifts = await db.Shifts.AsNoTracking().Where(x => x.Status == RecordStatusEnum.Active).OrderBy(x => x.StartTime)
            .Select(x => new ProductionLookupOption(x.Id, x.Code, x.Name, x.SiteId, null, x.StartTime, x.EndTime, x.SiteId)).ToArrayAsync(ct);
        var reasons = await db.DowntimeReasons.AsNoTracking().Where(x => x.Status == RecordStatusEnum.Active).OrderBy(x => x.Code)
            .Select(x => new ProductionLookupOption(x.Id, x.Code, x.Name)).ToArrayAsync(ct);
        return new ProductionLookups(workUnits, workCenters, products, shifts, reasons);
    }

    /// <summary>Đọc plan theo filter và phân trang, gồm actual quantity/has-output.</summary>
    public async Task<PagedResult<ProductionPlanListItem>> PlansAsync(ProductionPlanListQuery request, CancellationToken ct)
    {
        var (page, pageSize) = Page(request.Page, request.PageSize);
        var query = db.CurrentProductionPlans().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x => x.Code.Contains(search) || x.Product.Code.Contains(search) || x.WorkUnit.Code.Contains(search));
        }
        if (request.WorkUnitId.HasValue) query = query.Where(x => x.WorkUnitId == request.WorkUnitId);
        if (request.ProductId.HasValue) query = query.Where(x => x.ProductId == request.ProductId);
        if (request.From.HasValue) query = query.Where(x => x.PlannedEnd > request.From.Value);
        if (request.Until.HasValue) query = query.Where(x => x.PlannedStart < request.Until.Value);
        var total = await query.CountAsync(ct);
        var asOf = clock.UtcNow;
        var ordered = OrderPlans(query, request.SortBy, request.Descending, asOf).ThenBy(x => x.Id);
        var rows = await ordered.Skip((page - 1) * pageSize).Take(pageSize).Select(x => new ProductionModulePlanRow(
            x.Id, x.Code, x.WorkUnitId, x.WorkUnit.Code, x.ProductId, x.Product.Code, x.ShiftId,
            x.Shift != null ? x.Shift.Code : null, x.PlannedStart, x.PlannedEnd, x.PlannedQuantity,
            db.ProductionOutputs.Where(output => output.ProductionPlanId == x.Id).Sum(output => (decimal?)output.TotalProducedQuantity) ?? 0,
            x.Version, x.RowVersion, db.ProductionOutputs.Any(output => output.ProductionPlanId == x.Id),
            x.WorkUnit.Name, x.Product.Name)).ToArrayAsync(ct);
        return new PagedResult<ProductionPlanListItem>(rows.Select(row => new ProductionPlanListItem(
            row.Id, row.Code, row.WorkUnitId, row.WorkUnit, row.ProductId, row.Product, row.ShiftId, row.Shift,
            row.Start, row.End, row.Planned, row.Actual, row.Planned == 0 ? null : row.Actual / row.Planned,
            row.Version, Convert.ToBase64String(row.RowVersion), row.HasOutput,
            ProductionPlanExecutionRules.GetStatus(row.Start, row.End, row.Planned, row.Actual, row.HasOutput, asOf))
        {
            WorkUnitName = row.WorkUnitName,
            ProductName = row.ProductName
        }).ToArray(), total);
    }

    /// <summary>Đọc downtime có filter open/unclassified/time và paging.</summary>
    public async Task<PagedResult<DowntimeListItem>> DowntimeAsync(DowntimeListQuery request, CancellationToken ct)
    {
        var (page, pageSize) = Page(request.Page, request.PageSize);
        var query = FilterDowntime(request);
        var total = await query.CountAsync(ct);
        var ordered = OrderDowntime(query, request.SortBy, request.Descending);
        return new PagedResult<DowntimeListItem>(await LoadDowntimesAsync(ordered.Skip((page - 1) * pageSize).Take(pageSize), ct), total);
    }

    public async Task<DowntimeListItem> DowntimeAsync(Guid id, CancellationToken ct)
    {
        var items = await LoadDowntimesAsync(db.DowntimeEvents.AsNoTracking().Where(x => x.Id == id), ct);
        return items.SingleOrDefault() ?? throw new KeyNotFoundException("Downtime event was not found.");
    }

    /// <summary>Đọc mẫu cha cùng số dòng/máy/ca và tổng sản lượng bằng aggregate SQL.</summary>
    public async Task<PagedResult<SamplePlanListItem>> SamplesAsync(SamplePlanListQuery request, CancellationToken ct)
    {
        var (page, pageSize) = Page(request.Page, request.PageSize);
        var query = db.SampleProductionPlans.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x => x.Code.Contains(search) || x.Name.Contains(search));
        }
        var total = await query.CountAsync(ct);
        var ordered = OrderSamples(query, request.SortBy, request.Descending);
        var rows = await ordered.Skip((page - 1) * pageSize).Take(pageSize).Select(x => new ProductionModuleSampleRow(
            x.Id, x.Code, x.Name, x.Description,
            db.SampleProductionPlanItems.Count(item => item.SampleProductionPlanId == x.Id),
            db.SampleProductionPlanItems.Where(item => item.SampleProductionPlanId == x.Id).Select(item => item.WorkUnitId).Distinct().Count(),
            db.SampleProductionPlanItems.Where(item => item.SampleProductionPlanId == x.Id).Select(item => item.ShiftId).Distinct().Count(),
            db.SampleProductionPlanItems.Where(item => item.SampleProductionPlanId == x.Id).Sum(item => (decimal?)item.PlannedQuantity) ?? 0,
            x.UpdatedAt ?? x.CreatedAt, x.RowVersion, x.UpdatedBy ?? x.CreatedBy)).ToArrayAsync(ct);
        return new PagedResult<SamplePlanListItem>(rows.Select(x => new SamplePlanListItem(
            x.Id, x.Code, x.Name, x.Description, x.ItemCount, x.WorkUnitCount, x.ShiftCount,
            x.TotalQuantity, x.UpdatedAt, Convert.ToBase64String(x.RowVersion), x.UpdatedBy)).ToArray(), total);
    }

    /// <summary>Đọc phần cha và một summary aggregate của mẫu.</summary>
    public async Task<SamplePlanDetail> SampleAsync(Guid id, CancellationToken ct)
    {
        var sample = await db.SampleProductionPlans.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Sample production plan was not found.");
        var summary = await db.SampleProductionPlanItems.AsNoTracking().Where(x => x.SampleProductionPlanId == id)
            .GroupBy(_ => 1).Select(group => new
            {
                Count = group.Count(),
                WorkUnits = group.Select(x => x.WorkUnitId).Distinct().Count(),
                Shifts = group.Select(x => x.ShiftId).Distinct().Count(),
                Total = group.Sum(x => x.PlannedQuantity),
            }).SingleOrDefaultAsync(ct);
        return new SamplePlanDetail(sample.Id, sample.Code, sample.Name, sample.Description,
            summary?.Count ?? 0, summary?.WorkUnits ?? 0, summary?.Shifts ?? 0, summary?.Total ?? 0,
            Convert.ToBase64String(sample.RowVersion));
    }

    /// <summary>Đọc các dòng mẫu theo trang và trả RowVersion Base64.</summary>
    public async Task<PagedResult<SamplePlanItemDetail>> SampleItemsAsync(Guid samplePlanId, string? sortBy, bool descending, int page, int pageSize, CancellationToken ct)
    {
        (page, pageSize) = Page(page, pageSize);
        var query = db.SampleProductionPlanItems.AsNoTracking().Where(x => x.SampleProductionPlanId == samplePlanId);
        var total = await query.CountAsync(ct);
        var ordered = sortBy?.ToLowerInvariant() switch
        {
            "product" => descending ? query.OrderByDescending(x => x.Product.Code) : query.OrderBy(x => x.Product.Code),
            "shift" => descending ? query.OrderByDescending(x => x.Shift.StartTime) : query.OrderBy(x => x.Shift.StartTime),
            "quantity" => descending ? query.OrderByDescending(x => x.PlannedQuantity) : query.OrderBy(x => x.PlannedQuantity),
            _ => descending ? query.OrderByDescending(x => x.WorkUnit.Code).ThenBy(x => x.Shift.StartTime).ThenBy(x => x.Product.Code)
                : query.OrderBy(x => x.WorkUnit.Code).ThenBy(x => x.Shift.StartTime).ThenBy(x => x.Product.Code),
        };
        var rows = await ordered.ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).Select(x => new ProductionModuleSampleItemRow(
            x.Id, x.WorkUnitId, x.WorkUnit.Code, x.ProductId, x.Product.Code, x.ShiftId, x.Shift.Code,
            x.PlannedQuantity, x.Notes, x.RowVersion, x.WorkUnit.Name, x.Product.Name, x.Shift.Name, x.Shift.StartTime, x.Shift.EndTime)).ToArrayAsync(ct);
        return new PagedResult<SamplePlanItemDetail>(rows.Select(x => new SamplePlanItemDetail(x.Id,
            x.WorkUnitId, x.WorkUnit, x.ProductId, x.Product, x.ShiftId, x.Shift, x.Quantity, x.Notes,
            Convert.ToBase64String(x.RowVersion), x.WorkUnitName, x.ProductName, x.ShiftName, x.ShiftStart, x.ShiftEnd)).ToArray(), total);
    }

    private IQueryable<DowntimeEventEntity> FilterDowntime(DowntimeListQuery request)
    {
        var query = db.DowntimeEvents.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim();
            query = query.Where(x => x.WorkUnit.Code.Contains(search) || x.DowntimeReason.Code.Contains(search)
                || x.DowntimeReason.Name.Contains(search) || x.Note != null && x.Note.Contains(search));
        }
        if (request.WorkUnitId.HasValue) query = query.Where(x => x.WorkUnitId == request.WorkUnitId);
        if (request.DowntimeReasonId.HasValue) query = query.Where(x => x.DowntimeReasonId == request.DowntimeReasonId);
        if (request.UnclassifiedOnly) query = query.Where(x => x.DowntimeReason.Code == "DR-UNKNOWN");
        if (request.OpenOnly) query = query.Where(x => x.EndAt == null);
        if (request.From.HasValue) query = query.Where(x => x.EndAt == null || x.EndAt > request.From.Value);
        if (request.Until.HasValue) query = query.Where(x => x.StartAt < request.Until.Value);
        return query;
    }

    private static async Task<DowntimeListItem[]> LoadDowntimesAsync(IQueryable<DowntimeEventEntity> query, CancellationToken ct)
    {
        var rows = await query.Select(x => new ProductionModuleDowntimeRow(x.Id, x.WorkUnitId, x.WorkUnit.Code, x.DowntimeReasonId,
            x.DowntimeReason.Code, x.DowntimeReason.Name, x.StartAt, x.EndAt, x.Category, x.Kind, x.Source,
            x.Note, x.ProductionOutputId, x.UpdatedAt)).ToArrayAsync(ct);
        return rows.Select(x => new DowntimeListItem(x.Id, x.WorkUnitId, x.WorkUnit, x.ReasonId, x.ReasonCode,
            x.Reason, x.StartAt, x.EndAt, x.EndAt.HasValue ? (decimal)(x.EndAt.Value - x.StartAt).TotalMinutes : null,
            x.Category.ToString(), x.Kind.ToString(), x.Source.ToString(), x.Note, x.OutputId, Token(x.UpdatedAt))).ToArray();
    }

    private IOrderedQueryable<ProductionPlanEntity> OrderPlans(IQueryable<ProductionPlanEntity> query, string? sort, bool descending, DateTimeOffset asOf) => sort?.ToLowerInvariant() switch
    {
        "code" => descending ? query.OrderByDescending(x => x.Code) : query.OrderBy(x => x.Code),
        "workunit" => descending ? query.OrderByDescending(x => x.WorkUnit.Code) : query.OrderBy(x => x.WorkUnit.Code),
        "product" => descending ? query.OrderByDescending(x => x.Product.Code) : query.OrderBy(x => x.Product.Code),
        "quantity" => descending ? query.OrderByDescending(x => x.PlannedQuantity) : query.OrderBy(x => x.PlannedQuantity),
        "status" => OrderPlansByExecution(query, descending, asOf),
        _ => descending ? query.OrderByDescending(x => x.PlannedStart) : query.OrderBy(x => x.PlannedStart),
    };

    private IOrderedQueryable<ProductionPlanEntity> OrderPlansByExecution(IQueryable<ProductionPlanEntity> query, bool descending, DateTimeOffset asOf)
    {
        // Bản dịch SQL của ProductionPlanExecutionRules, để sort trước Skip/Take trên toàn bộ kết quả.
        System.Linq.Expressions.Expression<Func<ProductionPlanEntity, ProductionPlanExecutionStatusEnum>> status = x =>
            asOf < x.PlannedStart ? ProductionPlanExecutionStatusEnum.Pending :
            asOf < x.PlannedEnd ? ProductionPlanExecutionStatusEnum.InProgress :
            !db.ProductionOutputs.Any(output => output.ProductionPlanId == x.Id) ? ProductionPlanExecutionStatusEnum.MissingOutput :
            (db.ProductionOutputs.Where(output => output.ProductionPlanId == x.Id)
                .Sum(output => (decimal?)output.TotalProducedQuantity) ?? 0) < x.PlannedQuantity
                ? ProductionPlanExecutionStatusEnum.CompletedUnderTarget : ProductionPlanExecutionStatusEnum.CompletedOnTarget;
        return descending ? query.OrderByDescending(status) : query.OrderBy(status);
    }

    private static IOrderedQueryable<DowntimeEventEntity> OrderDowntime(IQueryable<DowntimeEventEntity> query, string? sort, bool descending) => sort?.ToLowerInvariant() switch
    {
        "workunit" => descending ? query.OrderByDescending(x => x.WorkUnit.Code) : query.OrderBy(x => x.WorkUnit.Code),
        "reason" => descending ? query.OrderByDescending(x => x.DowntimeReason.Code) : query.OrderBy(x => x.DowntimeReason.Code),
        "kind" => descending ? query.OrderByDescending(x => x.Kind) : query.OrderBy(x => x.Kind),
        _ => descending ? query.OrderByDescending(x => x.StartAt) : query.OrderBy(x => x.StartAt),
    };

    private static IOrderedQueryable<SampleProductionPlanEntity> OrderSamples(IQueryable<SampleProductionPlanEntity> query, string? sort, bool descending) => sort?.ToLowerInvariant() switch
    {
        "name" => descending ? query.OrderByDescending(x => x.Name) : query.OrderBy(x => x.Name),
        "updated" => descending ? query.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt) : query.OrderBy(x => x.UpdatedAt ?? x.CreatedAt),
        _ => descending ? query.OrderByDescending(x => x.Code) : query.OrderBy(x => x.Code),
    };

    private static (int Page, int PageSize) Page(int page, int pageSize) => (Math.Max(1, page), Math.Clamp(pageSize, 1, 100));
    private static string Token(DateTimeOffset? updatedAt) => updatedAt?.ToUniversalTime().ToString("O") ?? "initial";

}
