using Microsoft.EntityFrameworkCore;
using Oee.Application.Chat;
using Oee.Application.Common;
using Oee.Application.Contracts.Modules;
using Oee.Domain.Entities.Production;
using Oee.Infrastructure.Operations;
using Oee.Infrastructure.Persistence;

namespace Oee.Infrastructure.Chat;

/// <summary>Chỉ projection dữ liệu cho chat; không xuất mật khẩu, secret, audit JSON hoặc chạy SQL động.</summary>
public sealed class EfChatReadStore(OeeDbContext db, IClock clock) : IChatReadStore
{
    public async Task<ChatCatalogPage> CatalogAsync(ChatCatalogKind kind, Guid? workUnitId, DateTimeOffset from,
        DateTimeOffset until, DateOnly effectiveOn, int take, CancellationToken ct)
    {
        IQueryable<ChatCatalogRow> rows = kind switch
        {
            ChatCatalogKind.Enterprise => db.Enterprises.AsNoTracking().OrderBy(x => x.Code).Select(x => new ChatCatalogRow(x.Code, x.Name, x.Description, null, null)),
            ChatCatalogKind.Site => db.Sites.AsNoTracking().OrderBy(x => x.Code).Select(x => new ChatCatalogRow(x.Code, x.Name, x.Enterprise.Code + " · " + x.TimeZoneId, null, null)),
            ChatCatalogKind.Area => db.Areas.AsNoTracking().OrderBy(x => x.Code).Select(x => new ChatCatalogRow(x.Code, x.Name, x.Site.Code, null, null)),
            ChatCatalogKind.WorkCenter => db.WorkCenters.AsNoTracking().OrderBy(x => x.Code).Select(x => new ChatCatalogRow(x.Code, x.Name, x.Area.Code, null, null)),
            ChatCatalogKind.WorkUnit => db.WorkUnits.AsNoTracking().Where(x => workUnitId == null || x.Id == workUnitId)
                .OrderBy(x => x.Code).Select(x => new ChatCatalogRow(x.Code, x.Name, x.WorkCenter.Area.Site.Enterprise.Code + " / " + x.WorkCenter.Area.Site.Code + " / " + x.WorkCenter.Area.Code + " / " + x.WorkCenter.Code, null, null)),
            ChatCatalogKind.UnmappedWorkUnit => db.WorkUnits.AsNoTracking().Where(x => (workUnitId == null || x.Id == workUnitId) && !db.PlcDeviceMappings.Any(m => m.WorkUnitId == x.Id && m.IsActive))
                .OrderBy(x => x.Code).Select(x => new ChatCatalogRow(x.Code, x.Name, "Chưa có mapping hoạt động", null, null)),
            ChatCatalogKind.Product => db.Products.AsNoTracking().OrderBy(x => x.Code).Select(x => new ChatCatalogRow(x.Code, x.Name, x.DefaultUom, null, null)),
            ChatCatalogKind.Shift => db.Shifts.AsNoTracking().OrderBy(x => x.Code).Select(x => new ChatCatalogRow(x.Code, x.Name, x.Site.Code + " · " + x.StartTime.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture) + "–" + x.EndTime.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture), null, null)),
            ChatCatalogKind.Standard => db.WorkUnitStandards.AsNoTracking().Where(x => (workUnitId == null || x.WorkUnitId == workUnitId)
                    && x.EffectiveFrom <= effectiveOn && (x.EffectiveTo == null || x.EffectiveTo >= effectiveOn))
                .OrderBy(x => x.WorkUnit.Code).Select(x => new ChatCatalogRow(x.WorkUnit.Code, x.Product.Code, x.Uom + "/phút", x.StandardSpeedPerMinute, null)),
            ChatCatalogKind.User => db.Users.AsNoTracking().OrderBy(x => x.UserName).Select(x => new ChatCatalogRow(x.UserName ?? "", "Tài khoản", null, null, null)),
            ChatCatalogKind.DowntimeReason => db.DowntimeReasons.AsNoTracking().OrderBy(x => x.Code).Select(x => new ChatCatalogRow(x.Code, x.Name, x.Category.ToString() + " / " + x.Kind.ToString(), null, null)),
            ChatCatalogKind.DeviceMapping => db.PlcDeviceMappings.AsNoTracking().Where(x => workUnitId == null || x.WorkUnitId == workUnitId)
                .OrderBy(x => x.ExternalDeviceId).Select(x => new ChatCatalogRow(x.ExternalDeviceId, x.WorkUnit.Code, x.OpcUaConnection.Code + (x.IsActive ? " · hoạt động" : " · tạm ngưng"), null, null)),
            ChatCatalogKind.FaultMapping => db.PlcFaultCodeMappings.AsNoTracking().Where(x => workUnitId == null || x.WorkUnitId == workUnitId || x.WorkUnitId == null)
                .OrderBy(x => x.FaultCode).Select(x => new ChatCatalogRow(x.FaultCode, x.DowntimeReason.Name, x.WorkUnit != null ? x.WorkUnit.Code : "Global", null, null)),
            ChatCatalogKind.Audit => db.AuditLogs.AsNoTracking().Where(x => x.Timestamp >= from && x.Timestamp < until)
                .OrderByDescending(x => x.Timestamp).Select(x => new ChatCatalogRow(x.Action, x.EntityType + " · " + (x.EntitySubject ?? x.EntityId), x.UserName, null, x.Timestamp)),
            ChatCatalogKind.AlertStatistics => db.OeeAlerts.AsNoTracking().Where(x => x.TriggeredAt >= from && x.TriggeredAt < until && (workUnitId == null || x.WorkUnitId == workUnitId))
                .GroupBy(x => new { x.WorkUnit.Code, x.Metric, x.State }).OrderBy(x => x.Key.Code).Select(g => new ChatCatalogRow(g.Key.Code, g.Key.Metric.ToString(), g.Key.State.ToString(), g.Count(), null)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        var total = await rows.CountAsync(ct);
        var items = await rows.Take(Math.Clamp(take, 1, 20)).ToArrayAsync(ct);
        int? totalEvents = kind == ChatCatalogKind.AlertStatistics
            ? await db.OeeAlerts.AsNoTracking().CountAsync(x => x.TriggeredAt >= from && x.TriggeredAt < until && (workUnitId == null || x.WorkUnitId == workUnitId), ct)
            : null;
        return new(items, total, totalEvents);
    }

    public async Task<ChatPlanSummary> PlansAsync(Guid? workUnitId, DateTimeOffset from, DateTimeOffset until,
        bool startsAfter, int take, CancellationToken ct)
    {
        var plans = db.CurrentProductionPlans().AsNoTracking().Where(x => (workUnitId == null || x.WorkUnitId == workUnitId)
            && x.PlannedStart < until && (startsAfter ? x.PlannedStart >= from : x.PlannedEnd > from));
        var count = await plans.CountAsync(ct);
        var planned = await plans.SumAsync(x => (decimal?)x.PlannedQuantity, ct) ?? 0;
        var actual = await db.ProductionOutputs.AsNoTracking().Where(x => plans.Select(p => p.Id).Contains(x.ProductionPlanId))
            .SumAsync(x => (decimal?)x.TotalProducedQuantity, ct) ?? 0;
        var values = await plans.OrderBy(x => x.PlannedStart).ThenBy(x => x.Id).Take(Math.Clamp(take, 1, 20))
            .Select(x => new ProductionModulePlanRow(x.Id, x.Code, x.WorkUnitId, x.WorkUnit.Code, x.ProductId, x.Product.Code,
                x.ShiftId, x.Shift != null ? x.Shift.Code : null, x.PlannedStart, x.PlannedEnd, x.PlannedQuantity,
                db.ProductionOutputs.Where(o => o.ProductionPlanId == x.Id).Sum(o => (decimal?)o.TotalProducedQuantity) ?? 0,
                x.Version, x.RowVersion, db.ProductionOutputs.Any(o => o.ProductionPlanId == x.Id),
                x.WorkUnit.Name, x.Product.Name)).ToArrayAsync(ct);
        var items = values.Select(x => new ProductionPlanListItem(x.Id, x.Code, x.WorkUnitId, x.WorkUnit, x.ProductId, x.Product,
            x.ShiftId, x.Shift, x.Start, x.End, x.Planned, x.Actual, x.Planned == 0 ? null : x.Actual / x.Planned,
            x.Version, Convert.ToBase64String(x.RowVersion), x.HasOutput,
            ProductionPlanExecutionRules.GetStatus(x.Start, x.End, x.Planned, x.Actual, x.HasOutput, clock.UtcNow))
        {
            WorkUnitName = x.WorkUnitName,
            ProductName = x.ProductName
        }).ToArray();
        return new(count, planned, actual, items);
    }

    public async Task<ChatTargetPage> TargetsAsync(Guid? workUnitId, DateOnly effectiveOn, int take, CancellationToken ct, DateOnly? expiresUntil = null)
    {
        var siteId = workUnitId.HasValue ? await db.WorkUnits.AsNoTracking().Where(x => x.Id == workUnitId)
            .Select(x => (Guid?)x.WorkCenter.Area.SiteId).SingleOrDefaultAsync(ct) : null;
        var query = db.OeeTargets.AsNoTracking().Where(x => x.EffectiveFrom <= effectiveOn && (x.EffectiveTo == null || x.EffectiveTo >= effectiveOn));
        if (expiresUntil.HasValue) query = query.Where(x => x.EffectiveTo != null && x.EffectiveTo <= expiresUntil);
        if (workUnitId.HasValue) query = query.Where(x => x.WorkUnitId == workUnitId || siteId != null && x.SiteId == siteId || x.WorkUnitId == null && x.SiteId == null);
        var count = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.WorkUnitId != null).ThenByDescending(x => x.SiteId != null).ThenBy(x => x.PeriodType).ThenByDescending(x => x.EffectiveFrom)
            .Take(Math.Clamp(take, 1, 20)).Select(x => new TargetListItem(x.Id, x.WorkUnitId, x.SiteId,
                x.WorkUnit != null ? x.WorkUnit.Code : x.Site != null ? x.Site.Code : "Global", x.PeriodType.ToString(), x.PeriodCode,
                x.MinimumOee, x.MinimumAvailability, x.MinimumPerformance, x.MinimumQuality, x.EffectiveFrom, x.EffectiveTo,
                null, true, "")).ToArrayAsync(ct);
        return new(items, count);
    }
}
