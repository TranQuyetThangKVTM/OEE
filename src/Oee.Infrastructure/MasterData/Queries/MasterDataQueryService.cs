using Microsoft.EntityFrameworkCore;
using Oee.Application.Common;
using Oee.Application.Contracts.Modules;
using Oee.Application.Modules;
using Oee.Domain.Common;
using Oee.Infrastructure.Persistence;

namespace Oee.Infrastructure.Operations;

/// <summary>
/// Query service đọc Master Data/Organization dưới dạng DTO cho các bảng và dialog MudBlazor.
/// </summary>
/// <remarks>
/// Mọi query dùng AsNoTracking và projection nên EF Core tạo SELECT chỉ gồm cột cần thiết. Search,
/// filter, sorting, Count và Skip/Take chạy tại SQL; UI không tải toàn bộ bảng để phân trang trong browser.
/// RowVersion được encode Base64 làm concurrency token, không gửi EF Entity đang tracking.
/// </remarks>
public sealed class MasterDataQueryService(DbContextOptions<OeeDbContext> dbOptions, IClock clock) : IMasterDataQueryService
{
    // Blazor Server có thể khởi chạy lookup và bảng server-side cùng lúc. Mỗi method dùng
    // DbContext riêng để các thao tác đọc song song không dùng chung ChangeTracker/connection.
    private OeeDbContext CreateDbContext() => new(dbOptions);

    // Các method theo Id tải lại dữ liệu mới nhất trước khi dialog update mở, tránh tin row DTO cũ.
    public async Task<ProductListItem> ProductAsync(Guid id, CancellationToken ct)
    {
        await using var db = CreateDbContext();
        return await db.Products.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new ProductListItem(x.Id, x.Code, x.Name, x.DefaultUom, x.Description, x.Status.ToString(),
                x.UpdatedAt ?? x.CreatedAt, Token(x.RowVersion))).SingleOrDefaultAsync(ct)
        ?? throw Missing("Product");
    }

    public async Task<WorkUnitStandardListItem> StandardAsync(Guid id, CancellationToken ct)
    {
        await using var db = CreateDbContext();
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        return await db.WorkUnitStandards.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new WorkUnitStandardListItem(x.Id, x.WorkUnitId, x.WorkUnit.Code, x.ProductId, x.Product.Code,
                x.StandardSpeedPerMinute, x.StandardSpeedPerMinute * 60m, x.Uom, x.EffectiveFrom, x.EffectiveTo,
                x.ApprovedBy, x.EffectiveFrom <= today && (!x.EffectiveTo.HasValue || x.EffectiveTo >= today),
                x.UpdatedAt ?? x.CreatedAt, Token(x.RowVersion))).SingleOrDefaultAsync(ct)
            ?? throw Missing("Work-unit standard");
    }

    public async Task<ShiftListItem> ShiftAsync(Guid id, CancellationToken ct)
    {
        await using var db = CreateDbContext();
        return await db.Shifts.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new ShiftListItem(x.Id, x.SiteId, x.Site.Code + " — " + x.Site.Name, x.Code, x.Name,
                x.StartTime, x.EndTime, x.EndTime < x.StartTime, x.Status.ToString(), x.UpdatedAt ?? x.CreatedAt,
                Token(x.RowVersion))).SingleOrDefaultAsync(ct) ?? throw Missing("Shift");
    }

    public async Task<DowntimeReasonListItem> DowntimeReasonAsync(Guid id, CancellationToken ct)
    {
        await using var db = CreateDbContext();
        return await db.DowntimeReasons.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new DowntimeReasonListItem(x.Id, x.Code, x.Name, x.Category.ToString(), x.Kind.ToString(),
                x.Description, x.Status.ToString(), x.Code == "DR-UNKNOWN", x.UpdatedAt ?? x.CreatedAt,
                Token(x.RowVersion))).SingleOrDefaultAsync(ct) ?? throw Missing("Downtime reason");
    }

    public async Task<WorkUnitListItem> WorkUnitAsync(Guid id, CancellationToken ct)
    {
        await using var db = CreateDbContext();
        return await db.WorkUnits.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new WorkUnitListItem(x.Id, x.WorkCenterId,
                x.WorkCenter.Area.Site.Code + " / " + x.WorkCenter.Area.Code + " / " + x.WorkCenter.Code,
                x.WorkCenter.Area.Code, x.WorkCenter.Area.Site.Code, x.Code, x.Name,
                db.PlcDeviceRuntimeStates.Where(r => r.PlcDeviceMapping.WorkUnitId == x.Id)
                    .Select(r => r.OperationalState.ToString()).FirstOrDefault() ?? x.OperationalState.ToString(),
                db.PlcDeviceRuntimeStates.Where(r => r.PlcDeviceMapping.WorkUnitId == x.Id)
                    .Select(r => r.ConnectivityState.ToString()).FirstOrDefault() ?? ConnectivityStateEnum.NotConfigured.ToString(),
                x.Status.ToString(), x.UpdatedAt ?? x.CreatedAt, Token(x.RowVersion), x.Description)).SingleOrDefaultAsync(ct)
            ?? throw Missing("Work unit");
    }

    public async Task<WorkCenterListItem> WorkCenterAsync(Guid id, CancellationToken ct)
    {
        await using var db = CreateDbContext();
        return await db.WorkCenters.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new WorkCenterListItem(x.Id, x.AreaId,
                x.Area.Site.Code + " / " + x.Area.Code + " — " + x.Area.Name, x.Area.Site.Code,
                x.Code, x.Name, db.WorkUnits.Count(w => w.WorkCenterId == x.Id), x.Status.ToString(),
                x.UpdatedAt ?? x.CreatedAt, Token(x.RowVersion), x.Description)).SingleOrDefaultAsync(ct)
            ?? throw Missing("Work center");
    }

    public async Task<AreaListItem> AreaAsync(Guid id, CancellationToken ct)
    {
        await using var db = CreateDbContext();
        return await db.Areas.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new AreaListItem(x.Id, x.SiteId, x.Site.Code + " — " + x.Site.Name, x.Code, x.Name,
                db.WorkCenters.Count(g => g.AreaId == x.Id), x.Status.ToString(), x.UpdatedAt ?? x.CreatedAt,
                Token(x.RowVersion), x.Description)).SingleOrDefaultAsync(ct) ?? throw Missing("Area");
    }

    public async Task<EnterpriseListItem> EnterpriseAsync(Guid id, CancellationToken ct)
    {
        await using var db = CreateDbContext();
        return await db.Enterprises.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new EnterpriseListItem(
                x.Id, x.Code, x.Name, x.Description,
                db.Sites.Count(s => s.EnterpriseId == x.Id), x.Status.ToString(), x.UpdatedAt ?? x.CreatedAt,
                Token(x.RowVersion)
                )).SingleOrDefaultAsync(ct) ?? throw Missing("Enterprise");
    }

    public async Task<SiteListItem> SiteAsync(Guid id, CancellationToken ct)
    {
        await using var db = CreateDbContext();
        return await db.Sites.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new SiteListItem(x.Id, x.EnterpriseId, x.Enterprise.Code + " — " + x.Enterprise.Name,
                x.Code, x.Name, x.TimeZoneId, db.Areas.Count(p => p.SiteId == x.Id),
                db.Shifts.Count(s => s.SiteId == x.Id), x.Status.ToString(), x.UpdatedAt ?? x.CreatedAt,
                Token(x.RowVersion), x.Description)).SingleOrDefaultAsync(ct) ?? throw Missing("Site");
    }

    /// <summary>Đọc lookup tổ chức/master cùng enum/timezone phục vụ các form.</summary>
    public async Task<MasterDataLookups> LookupsAsync(CancellationToken ct)
    {
        await using var db = CreateDbContext();
        // Mỗi projection kèm IsActive để component chỉ cho chọn parent/SKU còn hiệu lực.
        var enterprises = await db.Enterprises.AsNoTracking().OrderBy(x => x.Code)
            .Select(x => new MasterLookupOption(x.Id, x.Code, x.Name, null, null, x.Status == RecordStatusEnum.Active)).ToArrayAsync(ct);
        var sites = await db.Sites.AsNoTracking().OrderBy(x => x.Code)
            .Select(x => new MasterLookupOption(x.Id, x.Code, x.Name, x.EnterpriseId, x.Enterprise.Name, x.Status == RecordStatusEnum.Active)).ToArrayAsync(ct);
        var areas = await db.Areas.AsNoTracking().OrderBy(x => x.Code)
            .Select(x => new MasterLookupOption(x.Id, x.Code, x.Name, x.SiteId, x.Site.Name, x.Status == RecordStatusEnum.Active)).ToArrayAsync(ct);
        var workCenters = await db.WorkCenters.AsNoTracking().OrderBy(x => x.Code)
            .Select(x => new MasterLookupOption(x.Id, x.Code, x.Name, x.AreaId, x.Area.Name, x.Status == RecordStatusEnum.Active)).ToArrayAsync(ct);
        var workUnits = await db.WorkUnits.AsNoTracking().OrderBy(x => x.Code)
            .Select(x => new MasterLookupOption(x.Id, x.Code, x.Name, x.WorkCenterId, x.WorkCenter.Name, x.Status == RecordStatusEnum.Active)).ToArrayAsync(ct);
        var products = await db.Products.AsNoTracking().OrderBy(x => x.Code)
            .Select(x => new MasterLookupOption(x.Id, x.Code, x.Name, null, null, x.Status == RecordStatusEnum.Active)).ToArrayAsync(ct);
        var statuses = Enum.GetValues<RecordStatusEnum>().Select(x => new MasterLookupOption(Guid.Empty, x.ToString(), x.ToString())).ToArray();
        var categories = Enum.GetValues<DowntimeCategoryEnum>().Select(x => new MasterLookupOption(Guid.Empty, x.ToString(), x.ToString())).ToArray();
        var kinds = Enum.GetValues<DowntimeKindEnum>().Select(x => new MasterLookupOption(Guid.Empty, x.ToString(), x.ToString())).ToArray();
        var timeZones = TimeZoneInfo.GetSystemTimeZones().Select(x => x.Id).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        return new(enterprises, sites, areas, workCenters, workUnits, products, statuses, categories, kinds, timeZones);
    }

    /// <summary>Tìm, sort và phân trang Product tại SQL.</summary>
    public async Task<PagedResult<ProductListItem>> ProductsAsync(MasterListQuery request, CancellationToken ct)
    {
        await using var db = CreateDbContext();
        var (page, size) = Page(request);
        var query = db.Products.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(x => x.Code.Contains(term) || x.Name.Contains(term) || x.DefaultUom.Contains(term)
                || x.Description != null && x.Description.Contains(term));
        }
        if (ParseStatus(request.Status) is { } status) query = query.Where(x => x.Status == status);
        // Count dùng cùng filter trước khi áp dụng Skip/Take để pager phản ánh đúng tổng số.
        var count = await query.CountAsync(ct);
        query = (request.SortBy, request.Descending) switch
        {
            ("name", true) => query.OrderByDescending(x => x.Name),
            ("name", false) => query.OrderBy(x => x.Name),
            ("uom", true) => query.OrderByDescending(x => x.DefaultUom),
            ("uom", false) => query.OrderBy(x => x.DefaultUom),
            ("status", true) => query.OrderByDescending(x => x.Status),
            ("status", false) => query.OrderBy(x => x.Status),
            ("updated", true) => query.OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt),
            ("updated", false) => query.OrderBy(x => x.UpdatedAt ?? x.CreatedAt),
            (_, true) => query.OrderByDescending(x => x.Code),
            _ => query.OrderBy(x => x.Code),
        };
        var items = await query.Skip((page - 1) * size).Take(size).Select(x => new ProductListItem(x.Id, x.Code, x.Name,
            x.DefaultUom, x.Description, x.Status.ToString(), x.UpdatedAt ?? x.CreatedAt, Token(x.RowVersion))).ToArrayAsync(ct);
        return new(items, count);
    }

    /// <summary>Đọc các phiên bản tốc độ chuẩn, có filter “đang hiệu lực”.</summary>
    public async Task<PagedResult<WorkUnitStandardListItem>> StandardsAsync(MasterListQuery request, CancellationToken ct)
    {
        await using var db = CreateDbContext();
        var (page, size) = Page(request);
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var query = db.WorkUnitStandards.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(x => x.WorkUnit.Code.Contains(term) || x.WorkUnit.Name.Contains(term)
                || x.Product.Code.Contains(term) || x.Product.Name.Contains(term));
        }
        if (request.ParentId.HasValue) query = query.Where(x => x.WorkUnitId == request.ParentId);
        if (request.RelatedId.HasValue) query = query.Where(x => x.ProductId == request.RelatedId);
        if (request.EffectiveOnly) query = query.Where(x => x.EffectiveFrom <= today && (!x.EffectiveTo.HasValue || x.EffectiveTo >= today));
        var count = await query.CountAsync(ct);
        query = (request.SortBy, request.Descending) switch
        {
            ("product", true) => query.OrderByDescending(x => x.Product.Code),
            ("product", false) => query.OrderBy(x => x.Product.Code),
            ("speed", true) => query.OrderByDescending(x => x.StandardSpeedPerMinute),
            ("speed", false) => query.OrderBy(x => x.StandardSpeedPerMinute),
            ("effective", true) => query.OrderByDescending(x => x.EffectiveFrom),
            ("effective", false) => query.OrderBy(x => x.EffectiveFrom),
            (_, true) => query.OrderByDescending(x => x.WorkUnit.Code),
            _ => query.OrderBy(x => x.WorkUnit.Code),
        };
        var items = await query.Skip((page - 1) * size).Take(size).Select(x => new WorkUnitStandardListItem(x.Id,
            x.WorkUnitId, x.WorkUnit.Code, x.ProductId, x.Product.Code, x.StandardSpeedPerMinute,
            x.StandardSpeedPerMinute * 60m, x.Uom, x.EffectiveFrom, x.EffectiveTo, x.ApprovedBy,
            x.EffectiveFrom <= today && (!x.EffectiveTo.HasValue || x.EffectiveTo >= today), x.UpdatedAt ?? x.CreatedAt,
            Token(x.RowVersion))).ToArrayAsync(ct);
        return new(items, count);
    }

    /// <summary>Đọc Shift theo Site/status và đánh dấu ca qua nửa đêm.</summary>
    public async Task<PagedResult<ShiftListItem>> ShiftsAsync(MasterListQuery request, CancellationToken ct)
    {
        await using var db = CreateDbContext();
        var (page, size) = Page(request);
        var query = db.Shifts.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search)) { var term = request.Search.Trim(); query = query.Where(x => x.Code.Contains(term) || x.Name.Contains(term) || x.Site.Code.Contains(term)); }
        if (request.ParentId.HasValue) query = query.Where(x => x.SiteId == request.ParentId);
        if (ParseStatus(request.Status) is { } status) query = query.Where(x => x.Status == status);
        var count = await query.CountAsync(ct);
        query = (request.SortBy, request.Descending) switch
        {
            ("site", true) => query.OrderByDescending(x => x.Site.Code),
            ("site", false) => query.OrderBy(x => x.Site.Code),
            ("start", true) => query.OrderByDescending(x => x.StartTime),
            ("start", false) => query.OrderBy(x => x.StartTime),
            ("status", true) => query.OrderByDescending(x => x.Status),
            ("status", false) => query.OrderBy(x => x.Status),
            (_, true) => query.OrderByDescending(x => x.Code),
            _ => query.OrderBy(x => x.Code),
        };
        var items = await query.Skip((page - 1) * size).Take(size).Select(x => new ShiftListItem(x.Id, x.SiteId,
            x.Site.Code, x.Code, x.Name, x.StartTime, x.EndTime, x.EndTime < x.StartTime, x.Status.ToString(),
            x.UpdatedAt ?? x.CreatedAt, Token(x.RowVersion))).ToArrayAsync(ct);
        return new(items, count);
    }

    /// <summary>Đọc DowntimeReason theo category/status, gồm cờ DR-UNKNOWN.</summary>
    public async Task<PagedResult<DowntimeReasonListItem>> DowntimeReasonsAsync(MasterListQuery request, CancellationToken ct)
    {
        await using var db = CreateDbContext();
        var (page, size) = Page(request);
        var query = db.DowntimeReasons.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search)) { var term = request.Search.Trim(); query = query.Where(x => x.Code.Contains(term) || x.Name.Contains(term) || x.Description != null && x.Description.Contains(term)); }
        if (Enum.TryParse<DowntimeCategoryEnum>(request.Category, true, out var category)) query = query.Where(x => x.Category == category);
        if (ParseStatus(request.Status) is { } status) query = query.Where(x => x.Status == status);
        var count = await query.CountAsync(ct);
        query = (request.SortBy, request.Descending) switch
        {
            ("name", true) => query.OrderByDescending(x => x.Name),
            ("name", false) => query.OrderBy(x => x.Name),
            ("category", true) => query.OrderByDescending(x => x.Category),
            ("category", false) => query.OrderBy(x => x.Category),
            ("kind", true) => query.OrderByDescending(x => x.Kind),
            ("kind", false) => query.OrderBy(x => x.Kind),
            ("status", true) => query.OrderByDescending(x => x.Status),
            ("status", false) => query.OrderBy(x => x.Status),
            (_, true) => query.OrderByDescending(x => x.Code),
            _ => query.OrderBy(x => x.Code),
        };
        var items = await query.Skip((page - 1) * size).Take(size).Select(x => new DowntimeReasonListItem(x.Id,
            x.Code, x.Name, x.Category.ToString(), x.Kind.ToString(), x.Description, x.Status.ToString(),
            x.Code == "DR-UNKNOWN", x.UpdatedAt ?? x.CreatedAt, Token(x.RowVersion))).ToArrayAsync(ct);
        return new(items, count);
    }

    // Chuẩn hóa paging để không nhận page âm hoặc request quá 100 dòng.
    private static (int Page, int Size) Page(MasterListQuery request) => (Math.Max(1, request.Page), Math.Clamp(request.PageSize, 1, 100));
    private static RecordStatusEnum? ParseStatus(string? value) => Enum.TryParse<RecordStatusEnum>(value, true, out var status) && Enum.IsDefined(status) ? status : null;
    private static string Token(byte[] value) => Convert.ToBase64String(value);
    private static KeyNotFoundException Missing(string name) => new($"{name} was not found.");
}
