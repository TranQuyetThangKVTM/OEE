using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Oee.Application.Common;
using Oee.Application.Contracts.Reporting;
using Oee.Application.Modules;
using Oee.Application.Reporting;
using Oee.Domain.Common;
using Oee.Infrastructure.Persistence;
using static Oee.Infrastructure.Reporting.ReportTimeResolver;

namespace Oee.Infrastructure.Reporting;

/// <summary>
/// Query engine dùng chung cho các màn hình phân tích và báo cáo PDF.
/// </summary>
/// <remarks>
/// Service chọn scope/timezone, tải fact bằng EF Core, tính KPI qua DashboardRollupCalculator và trả
/// một ReportData duy nhất cho UI lẫn PDF. Vì PDF không tự query/tính lại, A/P/Q/OEE không có
/// hai công thức khác nhau. DateOnly filter được resolve theo timezone Site, có xử lý ca qua nửa đêm.
/// </remarks>
public sealed class ReportAnalysisQueryService(
    OeeDbContext db,
    IClock clock) : IReportAnalysisQueryService
{
    private const int MaximumRangeDays = 366;

    /// <summary>Đọc lookup active cho bộ lọc báo cáo.</summary>
    public async Task<ReportFilterOptions> GetFilterOptionsAsync(CancellationToken ct)
    {
        var sites = await db.Sites.AsNoTracking().Where(x => x.Status == RecordStatusEnum.Active).OrderBy(x => x.Code)
            .Select(x => new ReportFilterOption(x.Id, x.Code, x.Name, null, x.TimeZoneId)).ToArrayAsync(ct);
        var areas = await db.Areas.AsNoTracking().Where(x => x.Status == RecordStatusEnum.Active).OrderBy(x => x.Code)
            .Select(x => new ReportFilterOption(x.Id, x.Code, x.Name, x.SiteId)).ToArrayAsync(ct);
        var groups = await db.WorkCenters.AsNoTracking().Where(x => x.Status == RecordStatusEnum.Active).OrderBy(x => x.Code)
            .Select(x => new ReportFilterOption(x.Id, x.Code, x.Name, x.AreaId)).ToArrayAsync(ct);
        var units = await db.WorkUnits.AsNoTracking().Where(x => x.Status == RecordStatusEnum.Active).OrderBy(x => x.Code)
            .Select(x => new ReportFilterOption(x.Id, x.Code, x.Name, x.WorkCenterId,
                x.WorkCenter.Area.Site.TimeZoneId)).ToArrayAsync(ct);
        var products = await db.Products.AsNoTracking().Where(x => x.Status == RecordStatusEnum.Active).OrderBy(x => x.Code)
            .Select(x => new ReportFilterOption(x.Id, x.Code, x.Name)).ToArrayAsync(ct);
        var shifts = await db.Shifts.AsNoTracking().Where(x => x.Status == RecordStatusEnum.Active).OrderBy(x => x.StartTime)
            .Select(x => new ReportFilterOption(x.Id, x.Code, x.Name, x.SiteId,
                x.StartTime.ToString("HH:mm", CultureInfo.InvariantCulture) + "–" + x.EndTime.ToString("HH:mm", CultureInfo.InvariantCulture))).ToArrayAsync(ct);
        var reasons = await db.DowntimeReasons.AsNoTracking().Where(x => x.Status == RecordStatusEnum.Active).OrderBy(x => x.Code)
            .Select(x => new ReportFilterOption(x.Id, x.Code, x.Name, null, x.Category.ToString())).ToArrayAsync(ct);

        var zoneId = sites.Select(x => x.Detail).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "Asia/Ho_Chi_Minh";
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.UtcNow, FindZone(zoneId)).DateTime);
        return new ReportFilterOptions(today, sites, areas, groups, units, products, shifts, reasons, clock.UtcNow);
    }

    /// <summary>Điều phối loại báo cáo, filter/timezone và trả dữ liệu đã phân trang.</summary>
    public async Task<ReportData> QueryAsync(ReportQuery query, CancellationToken ct)
    {
        // Scope được giữ dạng IQueryable để filter cây tổ chức chạy ở SQL.
        Validate(query);
        var scopeQuery = db.WorkUnits.AsNoTracking().Where(x => x.Status == RecordStatusEnum.Active);
        if (query.SiteId.HasValue) scopeQuery = scopeQuery.Where(x => x.WorkCenter.Area.SiteId == query.SiteId);
        if (query.AreaId.HasValue) scopeQuery = scopeQuery.Where(x => x.WorkCenter.AreaId == query.AreaId);
        if (query.WorkCenterId.HasValue) scopeQuery = scopeQuery.Where(x => x.WorkCenterId == query.WorkCenterId);
        if (query.WorkUnitId.HasValue) scopeQuery = scopeQuery.Where(x => x.Id == query.WorkUnitId);
        var selectedShift = query.ShiftId.HasValue && query.Kind is ReportKind.Reliability or ReportKind.DowntimeEvents
            ? await db.Shifts.AsNoTracking().Where(x => x.Id == query.ShiftId)
                .Select(x => new ReportTimelineShift(x.SiteId, x.StartTime, x.EndTime, x.Site.TimeZoneId)).SingleOrDefaultAsync(ct) : null;
        if (selectedShift is not null) scopeQuery = scopeQuery.Where(x => x.WorkCenter.Area.SiteId == selectedShift.SiteId);
        var scope = await scopeQuery.OrderBy(x => x.Code).Select(x => new ReportScopeRow(x.Id, x.Code, x.Name,
            x.WorkCenter.Area.SiteId, x.WorkCenter.Area.Site.TimeZoneId)).ToArrayAsync(ct);

        var zoneId = await ResolveZoneId(query, scope, ct);
        var zone = FindZone(zoneId);
        var bounds = Bounds(query.FromDate, query.ToDate, zone);
        if (selectedShift is not null)
            bounds = new ReportTimeBounds(LocalBoundary(query.FromDate, selectedShift.Start, zone),
                LocalBoundary(query.ToDate.AddDays(selectedShift.End <= selectedShift.Start ? 1 : 0), selectedShift.End, zone));
        else if (query.Kind == ReportKind.ShiftKpi || query.ShiftId.HasValue)
            bounds = bounds with { End = LocalBoundary(query.ToDate.AddDays(2), zone) };
        // Mỗi ReportKind có query chuyên biệt nhưng cùng trả ReportSection/ReportData contract.
        var section = scope.Length == 0
            ? ReportQuerySection.Empty
            : query.Kind switch
            {
                ReportKind.DowntimeEvents => await DowntimeEvents(query, scope, bounds, ct),
                ReportKind.QualityLoss => await QualityLoss(query, scope, bounds, zone, ct),
                ReportKind.Reliability => await Reliability(query, scope, bounds, ct),
                _ => await Kpi(query, scope, bounds, zone, ct),
            };

        return new ReportData(query.Kind, Title(query.Kind),
            FilterSummary(query, zoneId), zoneId, clock.UtcNow, section.Metrics, section.Trend,
            section.Breakdown, section.Rows, section.TotalCount, section.HasData, section.Timeline);
    }

    /// <summary>Dựng KPI ngày/tuần/tháng/ca và chi tiết thiết bị.</summary>
    private async Task<ReportQuerySection> Kpi(ReportQuery query, ReportScopeRow[] scope, ReportTimeBounds bounds, TimeZoneInfo zone, CancellationToken ct)
    {
        var ids = scope.Select(x => x.Id).ToArray();
        var periodType = query.Kind == ReportKind.ShiftKpi || query.ShiftId.HasValue ? OeePeriodTypeEnum.Shift : OeePeriodTypeEnum.Day;
        var snapshotQuery = db.OeeSnapshots.AsNoTracking().Where(x => ids.Contains(x.WorkUnitId)
            && x.PeriodType == periodType && x.PeriodStart < bounds.End && x.PeriodEnd > bounds.Start);
        if (query.ShiftId.HasValue)
        {
            var code = await db.Shifts.AsNoTracking().Where(x => x.Id == query.ShiftId).Select(x => x.Code).SingleOrDefaultAsync(ct);
            if (code is null) return ReportQuerySection.Empty;
            snapshotQuery = snapshotQuery.Where(x => x.PeriodCode.StartsWith("SHIFT-" + code + "-"));
        }
        var snapshots = await snapshotQuery.Select(x => new ReportSnapshotRaw(x.WorkUnitId, x.WorkUnit.Code,
            x.PeriodCode, x.PeriodType, x.PeriodStart, x.PeriodEnd, x.LoadingMinutes, x.UtilizationMinutes,
            x.NetUtilizationMinutes, x.TotalQuantity, x.DefectQuantity)).ToArrayAsync(ct);

        var outputQuery = db.ProductionOutputs.AsNoTracking().Where(x => ids.Contains(x.WorkUnitId)
            && x.PeriodStart < bounds.End && x.PeriodEnd > bounds.Start);
        if (query.ProductId.HasValue) outputQuery = outputQuery.Where(x => x.ProductId == query.ProductId);
        if (query.ShiftId.HasValue) outputQuery = outputQuery.Where(x => x.ShiftId == query.ShiftId);
        var outputs = await outputQuery.Select(x => new ReportOutputRaw(x.Id, x.WorkUnitId, x.WorkUnit.Code, x.ProductId,
            x.Product.Code, x.ShiftId, x.Shift != null ? x.Shift.Code : null, x.PeriodStart, x.PeriodEnd,
            x.StandardSpeedPerMinute, x.TotalProducedQuantity, x.DefectQuantity, x.UnscheduledMinutes)).ToArrayAsync(ct);

        var shifts = periodType == OeePeriodTypeEnum.Shift
            ? await db.Shifts.AsNoTracking().Where(x => scope.Select(s => s.SiteId).Contains(x.SiteId))
                .Select(x => new ReportShiftRaw(x.Id, x.Code, x.Name, x.StartTime, x.EndTime)).ToArrayAsync(ct)
            : [];
        if (periodType == OeePeriodTypeEnum.Shift)
        {
            snapshots = snapshots.Where(x =>
            {
                var date = OperationalDate(x.PeriodCode) ?? LocalDate(x.Start, zone);
                return date >= query.FromDate && date <= query.ToDate;
            }).ToArray();
            outputs = outputs.Where(x =>
            {
                var code = ShiftPeriodCode(x, shifts, zone); var date = OperationalDate(code);
                return date.HasValue && date.Value >= query.FromDate && date.Value <= query.ToDate;
            }).ToArray();
        }
        // Rollup nhận số tuyệt đối từ snapshot/output; ratio vẫn ở thang 0..1.
        var rows = BuildKpiRows(query.Kind, snapshots, outputs, shifts, zone);
        var all = Rollup(snapshots, outputs);
        var losses = await LoadDowntimeBreakdown(query, ids, bounds, ct);
        var trendRows = query.Kind is ReportKind.WeeklyKpi or ReportKind.MonthlyKpi
            ? rows.OrderBy(x => x.Code).Select(x => new ReportTrendPoint(x.Code ?? x.Label,
                x.Oee, x.Availability, x.Performance, x.Quality, x.Total, x.Good, x.Defect, x.DowntimeMinutes)).ToArray()
            : BuildKpiRows(ReportKind.DailyOee, snapshots.Where(x => x.PeriodType == OeePeriodTypeEnum.Day).ToArray(), outputs, shifts, zone)
                .GroupBy(x => x.Code).Select(g => CombineTrend(g.Key ?? string.Empty, g)).OrderBy(x => x.Label).ToArray();
        rows = rows.Select(row => row.WorkUnitId.HasValue
            ? row with { WorkUnit = scope.Where(x => x.Id == row.WorkUnitId).Select(x => x.Code + " · " + x.Name).FirstOrDefault() ?? row.WorkUnit }
            : row).ToArray();

        var metrics = new List<ReportMetric>
        {
            RatioMetric("oee", "OEE", all.Oee), RatioMetric("availability", "Availability", all.Availability),
            RatioMetric("performance", "Performance", all.Performance), RatioMetric("quality", "Quality", all.Quality),
            new("total", "Tổng sản lượng", all.TotalQuantity, "sp"), new("good", "Sản lượng đạt", all.GoodQuantity, "sp"),
            new("defect", "Sản lượng lỗi", all.RejectQuantity, "sp"), new("loading", "Thời gian tải", all.LoadingMinutes, "phút"),
        };
        if (query.Kind == ReportKind.DeviceOee)
        {
            var reliability = await Reliability(query, scope, bounds, ct);
            metrics.AddRange(reliability.Metrics.Where(x => x.Key is "mtbf" or "mttr" or "failures"));
        }
        if (query.Kind is ReportKind.WeeklyKpi or ReportKind.MonthlyKpi)
        {
            var values = rows.Where(x => x.Oee.HasValue).Select(x => x.Oee!.Value).ToArray();
            metrics =
            [
                RatioMetric("oee-average", "OEE trung bình", values.Length == 0 ? null : values.Average()),
                new("oee-high", query.Kind == ReportKind.WeeklyKpi ? "Tuần cao nhất" : "Tháng cao nhất", values.Length == 0 ? null : values.Max(), "%", true,
                    rows.Where(x => x.Oee.HasValue).OrderByDescending(x => x.Oee).FirstOrDefault()?.Label),
                new("oee-low", query.Kind == ReportKind.WeeklyKpi ? "Tuần thấp nhất" : "Tháng thấp nhất", values.Length == 0 ? null : values.Min(), "%", true,
                    rows.Where(x => x.Oee.HasValue).OrderBy(x => x.Oee).FirstOrDefault()?.Label),
                new("good", "Tổng SL đạt", rows.Sum(x => x.Good), "sp"),
            ];
        }

        return Page(query, rows, metrics, trendRows, losses);
    }

    /// <summary>Trả danh sách chi tiết downtime có server paging/sorting.</summary>
    private async Task<ReportQuerySection> DowntimeEvents(ReportQuery query, ReportScopeRow[] scope, ReportTimeBounds bounds, CancellationToken ct)
    {
        var events = await LoadDowntimes(query, scope.Select(x => x.Id).ToArray(), bounds, ct);
        var rows = events.Select(x => new ReportTableRow(x.Id, x.ReasonCode + " · " + x.Reason, x.WorkUnitId,
            x.WorkUnit, PeriodStart: x.Start, PeriodEnd: x.End, Code: x.ReasonCode,
            DowntimeMinutes: x.Minutes, EventCount: 1, Category: x.Category, Kind: x.Kind, Source: x.Source,
            Status: x.End.HasValue ? "Closed" : "Open", Note: x.Note,
            DrillDownUrl: "/production/downtime/" + x.Id)).ToArray();
        var total = events.Sum(x => x.Minutes); var planned = events.Where(x => x.Kind == DowntimeKindEnum.Planned.ToString()).Sum(x => x.Minutes);
        var metrics = new[]
        {
            new ReportMetric("events", "Số sự kiện", events.Length, "sự kiện"),
            new ReportMetric("total", "Tổng thời gian", total, "phút"),
            new ReportMetric("planned", "Có kế hoạch", planned, "phút"),
            new ReportMetric("unplanned", "Không kế hoạch", total - planned, "phút"),
        };
        var zone = FindZone(await ResolveZoneId(query, scope, ct));
        var timeline = await new ReportTimelineQuery(db, clock).LoadAsync(query, scope, bounds, zone, ct);
        return Page(query, rows, metrics, [], []) with { Timeline = timeline };
    }

    /// <summary>Tổng hợp Total/Good/Defect/Quality theo SKU.</summary>
    private async Task<ReportQuerySection> QualityLoss(ReportQuery query, ReportScopeRow[] scope, ReportTimeBounds bounds, TimeZoneInfo zone, CancellationToken ct)
    {
        var ids = scope.Select(x => x.Id).ToArray();
        var source = db.ProductionOutputs.AsNoTracking().Where(x => ids.Contains(x.WorkUnitId)
            && x.PeriodStart < bounds.End && x.PeriodEnd > bounds.Start);
        if (query.ProductId.HasValue) source = source.Where(x => x.ProductId == query.ProductId);
        if (query.ShiftId.HasValue) source = source.Where(x => x.ShiftId == query.ShiftId);
        var outputs = await source.Select(x => new ReportQualityRaw(x.ProductId, x.Product.Code, x.Product.Name,
            x.PeriodStart, x.TotalProducedQuantity, x.DefectQuantity)).ToArrayAsync(ct);
        var rows = outputs.GroupBy(x => new { x.ProductId, x.Code, x.Name }).Select(g =>
        {
            var total = g.Sum(x => x.Total); var defect = g.Sum(x => x.Defect);
            return new ReportTableRow(g.Key.ProductId, g.Key.Code + " · " + g.Key.Name, Product: g.Key.Code,
                Code: g.Key.Code, Quality: Divide(total - defect, total), Total: total, Good: total - defect,
                Defect: defect, Attainment: Divide(defect, total));
        }).ToArray();
        var totalAll = rows.Sum(x => x.Total); var defectAll = rows.Sum(x => x.Defect);
        var breakdown = rows.OrderByDescending(x => x.Defect).Select((x, index) => new ReportBreakdownPoint(
            x.Label, "Quality", 0, x.Defect, Divide(x.Defect, defectAll) ?? 0,
            Divide(rows.OrderByDescending(y => y.Defect).Take(index + 1).Sum(y => y.Defect), defectAll) ?? 0)).ToArray();
        var trend = outputs.GroupBy(x => LocalDate(x.At, zone)).OrderBy(x => x.Key).Select(g =>
        {
            var total = g.Sum(x => x.Total); var defect = g.Sum(x => x.Defect);
            return new ReportTrendPoint(g.Key.ToString("dd/MM", CultureInfo.InvariantCulture), null, null, null, Divide(total - defect, total),
                total, total - defect, defect, 0);
        }).ToArray();
        var metrics = new[]
        {
            RatioMetric("quality", "Quality", Divide(totalAll - defectAll, totalAll)),
            new ReportMetric("total", "Tổng sản lượng", totalAll, "sp"),
            new ReportMetric("defect", "Sản lượng lỗi", defectAll, "sp"),
            new ReportMetric("sku", "SKU có dữ liệu", rows.Length, "SKU"),
        };
        return Page(query, rows, metrics, trend, breakdown);
    }

    /// <summary>Tính MTBF/MTTR chỉ từ downtime Failure/Breakdown theo rule nghiệp vụ.</summary>
    private async Task<ReportQuerySection> Reliability(ReportQuery query, ReportScopeRow[] scope, ReportTimeBounds bounds, CancellationToken ct)
    {
        var zoneId = await ResolveZoneId(query, scope, ct);
        var timeline = await new ReportTimelineQuery(db, clock).LoadAsync(query, scope, bounds, FindZone(zoneId), ct);
        var rows = timeline.Rows.Select(unit =>
        {
            var down = unit.FailureMinutes; var up = unit.UptimeMinutes; var count = unit.FailureCount;
            return new ReportTableRow(unit.WorkUnitId, unit.Name, unit.WorkUnitId, unit.Code, Code: unit.Code,
                Availability: Divide(up, up + down), DowntimeMinutes: down, EventCount: count,
                MtbfMinutes: count == 0 ? null : up / count, MttrMinutes: count == 0 ? null : down / count,
                Kind: "Failure/Breakdown", UptimeMinutes: up);
        }).ToArray();
        var totalUp = rows.Sum(x => x.UptimeMinutes); var totalDown = rows.Sum(x => x.DowntimeMinutes); var count = rows.Sum(x => x.EventCount);
        var metrics = new[]
        {
            new ReportMetric("availability", "Availability", Divide(totalUp, totalUp + totalDown), "%", true, "Uptime / (Uptime + dừng do hỏng máy)"),
            new ReportMetric("mtbf", "MTBF", count == 0 ? null : totalUp / count, "phút", Description: "Trung bình thời gian chạy giữa các sự cố"),
            new ReportMetric("mttr", "MTTR", count == 0 ? null : totalDown / count, "phút", Description: "Trung bình thời gian khắc phục"),
            new ReportMetric("failures", "Số sự cố", count, "sự kiện", Description: "Chỉ tính Failure / Breakdown"),
        };
        return Page(query, rows, metrics, [], []) with { Timeline = timeline };
    }

    private async Task<ReportDowntimeRaw[]> LoadDowntimes(ReportQuery query, Guid[] ids, ReportTimeBounds bounds, CancellationToken ct)
    {
        var source = db.DowntimeEvents.AsNoTracking().Where(x => ids.Contains(x.WorkUnitId)
            && x.StartAt < bounds.End && (x.EndAt == null || x.EndAt > bounds.Start));
        if (query.ProductId.HasValue) source = source.Where(x => x.ProductionOutput != null && x.ProductionOutput.ProductId == query.ProductId);
        if (query.ShiftId.HasValue) source = source.Where(x => x.ProductionOutput == null || x.ProductionOutput.ShiftId == query.ShiftId);
        if (query.DowntimeReasonId.HasValue) source = source.Where(x => x.DowntimeReasonId == query.DowntimeReasonId);
        if (Enum.TryParse<DowntimeCategoryEnum>(query.DowntimeCategory, true, out var category)) source = source.Where(x => x.Category == category);
        if (Enum.TryParse<DataSourceEnum>(query.DataSource, true, out var dataSource)) source = source.Where(x => x.Source == dataSource);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            source = source.Where(x => x.WorkUnit.Code.Contains(term) || x.DowntimeReason.Code.Contains(term)
                || x.DowntimeReason.Name.Contains(term) || x.Note != null && x.Note.Contains(term));
        }
        var raw = await source.Select(x => new ReportDowntimeDbRaw(x.Id, x.WorkUnitId, x.WorkUnit.Code,
            x.DowntimeReasonId, x.DowntimeReason.Code, x.DowntimeReason.Name, x.StartAt, x.EndAt,
            x.Category, x.Kind, x.Source, x.Note)).ToArrayAsync(ct);
        var windows = query.ShiftId.HasValue
            ? await new ReportTimelineQuery(db, clock).TimelineWindows(query, bounds, FindZone(await ResolveZoneId(query, [], ct)), ct) : [bounds];
        return raw.Select(x => new ReportDowntimeRaw(x.Id, x.WorkUnitId, x.WorkUnit, x.ReasonId, x.ReasonCode,
            x.Reason, x.Start, x.End, windows.Sum(window => Duration(x.Start, x.End, window)), x.Category.ToString(), x.Kind.ToString(),
            x.Source.ToString(), x.Note)).Where(x => x.Minutes > 0).ToArray();
    }

    private async Task<ReportBreakdownPoint[]> LoadDowntimeBreakdown(ReportQuery query, Guid[] ids, ReportTimeBounds bounds, CancellationToken ct)
    {
        var events = await LoadDowntimes(query, ids, bounds, ct); var total = events.Sum(x => x.Minutes); decimal cumulative = 0;
        return events.GroupBy(x => new { x.ReasonId, x.ReasonCode, x.Reason, x.Category }).Select(g => new
        { g.Key, Count = g.Count(), Minutes = g.Sum(x => x.Minutes) }).OrderByDescending(x => x.Minutes).Take(20).Select(x =>
    {
        var percentage = Divide(x.Minutes, total) ?? 0; cumulative += percentage;
        return new ReportBreakdownPoint(x.Key.ReasonCode + " · " + x.Key.Reason, x.Key.Category, x.Count,
            x.Minutes, percentage, cumulative, "/reports/downtime-events?reasonId=" + x.Key.ReasonId);
    }).ToArray();
    }

    private static ReportTableRow[] BuildKpiRows(ReportKind kind, ReportSnapshotRaw[] snapshots, ReportOutputRaw[] outputs,
        ReportShiftRaw[] shifts, TimeZoneInfo zone)
    {
        var keys = new Dictionary<string, ReportBucket>(StringComparer.Ordinal);
        foreach (var snapshot in snapshots)
        {
            var key = BucketKey(kind, snapshot.WorkUnitId, snapshot.WorkUnit, snapshot.Start, snapshot.PeriodCode, zone);
            if (!keys.TryGetValue(key.Key, out var bucket)) keys[key.Key] = bucket = new ReportBucket(key.Label, key.Code, snapshot.WorkUnitId, snapshot.WorkUnit);
            bucket.Snapshots.Add(snapshot);
        }
        foreach (var output in outputs)
        {
            var periodCode = shifts.Length > 0 ? ShiftPeriodCode(output, shifts, zone) : null;
            if (kind == ReportKind.ShiftKpi && periodCode is null) continue;
            var key = BucketKey(kind, output.WorkUnitId, output.WorkUnit, output.Start, periodCode, zone);
            if (!keys.TryGetValue(key.Key, out var bucket)) keys[key.Key] = bucket = new ReportBucket(key.Label, key.Code, output.WorkUnitId, output.WorkUnit);
            bucket.Outputs.Add(output);
        }
        return keys.Values.Select(bucket =>
        {
            var rollup = Rollup(bucket.Snapshots, bucket.Outputs);
            var start = bucket.Snapshots.Select(x => x.Start).Concat(bucket.Outputs.Select(x => x.Start)).DefaultIfEmpty().Min();
            var end = bucket.Snapshots.Select(x => x.End).Concat(bucket.Outputs.Select(x => x.End)).DefaultIfEmpty().Max();
            var aggregatePeriod = kind is ReportKind.WeeklyKpi or ReportKind.MonthlyKpi;
            var shift = kind == ReportKind.ShiftKpi
                ? shifts.FirstOrDefault(x => bucket.Code?.StartsWith("SHIFT-" + x.Code + "-", StringComparison.Ordinal) == true)?.Name
                : null;
            return new ReportTableRow(null, bucket.Label, aggregatePeriod ? null : bucket.WorkUnitId,
                aggregatePeriod ? null : bucket.WorkUnit, Shift: shift,
                PeriodStart: start == default ? null : start, PeriodEnd: end == default ? null : end,
                Code: bucket.Code, Oee: rollup.Oee, Availability: rollup.Availability,
                Performance: rollup.Performance, Quality: rollup.Quality, Total: rollup.TotalQuantity,
                Good: rollup.GoodQuantity, Defect: rollup.RejectQuantity, LoadingMinutes: rollup.LoadingMinutes,
                DrillDownUrl: aggregatePeriod ? null : "/reports/device-detail?workUnitId=" + bucket.WorkUnitId);
        }).ToArray();
    }

    private static ReportBucketIdentity BucketKey(ReportKind kind, Guid unitId, string unit, DateTimeOffset at,
        string? periodCode, TimeZoneInfo zone)
    {
        var date = OperationalDate(periodCode) ?? LocalDate(at, zone);
        return kind switch
        {
            ReportKind.DailyOee => new($"{unitId}|D|{date:yyyyMMdd}", $"{date:dd/MM/yyyy} · {unit}", date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            ReportKind.WeeklyKpi => Week(date),
            ReportKind.MonthlyKpi => new($"M|{date:yyyyMM}", $"{date:MM/yyyy}", date.ToString("yyyy-MM", CultureInfo.InvariantCulture)),
            ReportKind.ShiftKpi => new($"{unitId}|S|{periodCode}", $"{periodCode} · {unit}", periodCode),
            _ => new(unitId.ToString(), unit, unit),
        };
    }

    private static ReportBucketIdentity Week(DateOnly date)
    {
        var value = date.ToDateTime(TimeOnly.MinValue); var year = ISOWeek.GetYear(value); var week = ISOWeek.GetWeekOfYear(value);
        return new($"W|{year:D4}{week:D2}", $"T{week:D2}/{year}", $"{year}-W{week:D2}");
    }

    private static string? ShiftPeriodCode(ReportOutputRaw output, ReportShiftRaw[] shifts, TimeZoneInfo zone)
    {
        if (!output.ShiftId.HasValue) return null;
        var shift = shifts.FirstOrDefault(x => x.Id == output.ShiftId); if (shift is null) return null;
        var local = TimeZoneInfo.ConvertTime(output.Start, zone); var date = DateOnly.FromDateTime(local.DateTime);
        if (shift.End <= shift.Start && TimeOnly.FromDateTime(local.DateTime) < shift.End) date = date.AddDays(-1);
        return $"SHIFT-{shift.Code}-{date:yyyyMMdd}";
    }

    private static DateOnly? OperationalDate(string? periodCode)
    {
        if (string.IsNullOrWhiteSpace(periodCode) || periodCode.Length < 8) return null;
        return DateOnly.TryParseExact(periodCode[^8..], "yyyyMMdd", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var value) ? value : null;
    }

    /// <summary>Chuyển row report về input của calculator Application dùng chung.</summary>
    private static DashboardRollupResult Rollup(IEnumerable<ReportSnapshotRaw> snapshots, IEnumerable<ReportOutputRaw> outputs)
    {
        var snapshotRows = snapshots.ToArray(); var outputRows = outputs.ToArray();
        var production = outputRows.Select(x => new DashboardProductionInput(x.Total, x.Defect,
            x.Speed <= 0 ? 0 : x.Total / x.Speed)).ToArray();
        if (production.Length == 0)
            production = snapshotRows.Select(x => new DashboardProductionInput(x.Total, x.Defect, x.NetUtilization)).ToArray();
        return DashboardRollupCalculator.Calculate(snapshotRows.Select(x => new DashboardSnapshotInput(x.Loading, x.Utilization)), production);
    }

    private static ReportTrendPoint CombineTrend(string label, IEnumerable<ReportTableRow> rows)
    {
        var source = rows.ToArray();
        return new ReportTrendPoint(label, Weighted(source, x => x.Oee), Weighted(source, x => x.Availability),
            Weighted(source, x => x.Performance), Weighted(source, x => x.Quality), source.Sum(x => x.Total),
            source.Sum(x => x.Good), source.Sum(x => x.Defect), source.Sum(x => x.DowntimeMinutes));
    }

    private static decimal? Weighted(ReportTableRow[] rows, Func<ReportTableRow, decimal?> selector)
    {
        var valid = rows.Where(x => selector(x).HasValue).ToArray(); if (valid.Length == 0) return null;
        var weight = valid.Sum(x => x.Total > 0 ? x.Total : 1);
        return valid.Sum(x => selector(x)!.Value * (x.Total > 0 ? x.Total : 1)) / weight;
    }

    /// <summary>Sắp xếp/phân trang kết quả đã aggregate và giữ TotalCount trước paging.</summary>
    private static ReportQuerySection Page(ReportQuery query, IEnumerable<ReportTableRow> source,
        IEnumerable<ReportMetric> metrics, IEnumerable<ReportTrendPoint> trend, IEnumerable<ReportBreakdownPoint> breakdown)
    {
        var rows = Sort(source, query.SortBy, query.Descending).ToArray();
        var page = Math.Max(1, query.Page); var size = Math.Clamp(query.PageSize, 1, 5000);
        var items = rows.Skip((page - 1) * size).Take(size).ToArray();
        var metricArray = metrics.ToArray(); var trendArray = trend.Take(120).ToArray(); var breakdownArray = breakdown.Take(50).ToArray();
        return new ReportQuerySection(metricArray, trendArray, breakdownArray, items, rows.Length,
            rows.Length > 0 || metricArray.Any(x => x.Value.HasValue && x.Value.Value != 0));
    }

    private static IOrderedEnumerable<ReportTableRow> Sort(IEnumerable<ReportTableRow> source, string? sort, bool descending)
    {
        Func<ReportTableRow, object?> key = sort?.ToLowerInvariant() switch
        {
            "date" => x => OperationalDate(x.Code)?.DayNumber,
            "workunit" => x => x.WorkUnit,
            "product" => x => x.Product,
            "oee" => x => x.Oee,
            "availability" => x => x.Availability,
            "performance" => x => x.Performance,
            "quality" => x => x.Quality,
            "total" => x => x.Total,
            "defect" => x => x.Defect,
            "downtime" => x => x.DowntimeMinutes,
            "events" => x => x.EventCount,
            "mtbf" => x => x.MtbfMinutes,
            "mttr" => x => x.MttrMinutes,
            "code" => x => x.Code,
            _ => x => x.PeriodStart,
        };
        return descending ? source.OrderByDescending(key).ThenBy(x => x.PeriodStart).ThenBy(x => x.WorkUnit).ThenBy(x => x.Label)
            : source.OrderBy(key).ThenBy(x => x.PeriodStart).ThenBy(x => x.WorkUnit).ThenBy(x => x.Label);
    }

    private async Task<string> ResolveZoneId(ReportQuery query, ReportScopeRow[] scope, CancellationToken ct)
    {
        if (query.SiteId.HasValue)
        {
            var selected = await db.Sites.AsNoTracking().Where(x => x.Id == query.SiteId).Select(x => x.TimeZoneId).SingleOrDefaultAsync(ct);
            if (!string.IsNullOrWhiteSpace(selected)) return selected;
        }
        var zones = scope.Select(x => x.TimeZoneId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return zones.Length == 1 ? zones[0] : "Asia/Ho_Chi_Minh";
    }

    private static void Validate(ReportQuery query)
    {
        if (!Enum.IsDefined(query.Kind)) throw new ArgumentOutOfRangeException(nameof(query), "Loại báo cáo không được hỗ trợ.");
        if (query.ToDate < query.FromDate) throw new ArgumentException("Ngày kết thúc phải từ ngày bắt đầu trở đi.");
        if (query.ToDate.DayNumber - query.FromDate.DayNumber + 1 > MaximumRangeDays)
            throw new ArgumentException($"Khoảng báo cáo tối đa {MaximumRangeDays} ngày.");
        if (query.Page < 1 || query.PageSize is < 1 or > 5000) throw new ArgumentOutOfRangeException(nameof(query), "Trang hoặc kích thước trang không hợp lệ.");
    }

    /// <summary>Đổi khoảng ngày địa phương inclusive thành UTC bounds.</summary>
    private static ReportTimeBounds Bounds(DateOnly from, DateOnly to, TimeZoneInfo zone)
    {
        var start = LocalBoundary(from, zone); var end = LocalBoundary(to.AddDays(1), zone);
        return new ReportTimeBounds(start, end);
    }

    /// <summary>Clip downtime vào report bounds; event mở kết thúc tạm tại min(now, bounds.End).</summary>
    private decimal Duration(DateTimeOffset start, DateTimeOffset? end, ReportTimeBounds bounds)
    {
        var clippedStart = start < bounds.Start ? bounds.Start : start;
        var effectiveEnd = end.HasValue && end.Value < clock.UtcNow ? end.Value : clock.UtcNow;
        var clippedEnd = effectiveEnd > bounds.End ? bounds.End : effectiveEnd;
        return clippedEnd <= clippedStart ? 0 : (decimal)(clippedEnd - clippedStart).TotalMinutes;
    }

    private static DateOnly LocalDate(DateTimeOffset value, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(value, zone).DateTime);
    private static decimal? Divide(decimal numerator, decimal denominator) => denominator == 0 ? null : Math.Clamp(numerator / denominator, 0, 1);
    private static ReportMetric RatioMetric(string key, string label, decimal? value) => new(key, label, value, "%", true);
    private static string FilterSummary(ReportQuery query, string zone) =>
        $"{query.FromDate:dd/MM/yyyy} – {query.ToDate:dd/MM/yyyy} · {zone}";
    private static string Title(ReportKind kind) => kind switch
    {
        ReportKind.DeviceOee => "Chi tiết OEE theo thiết bị",
        ReportKind.DailyOee => "KPI theo ngày",
        ReportKind.WeeklyKpi => "KPI theo tuần ISO",
        ReportKind.MonthlyKpi => "KPI theo tháng",
        ReportKind.ShiftKpi => "KPI theo ca",
        ReportKind.DowntimeEvents => "Sự kiện downtime",
        ReportKind.QualityLoss => "Tổn thất chất lượng",
        ReportKind.Reliability => "Độ tin cậy thiết bị",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
