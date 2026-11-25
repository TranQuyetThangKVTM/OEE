using Microsoft.EntityFrameworkCore;
using Oee.Application.Common;
using Oee.Application.Contracts.Modules;
using Oee.Application.Modules;
using Oee.Domain.Common;
using Oee.Infrastructure.Persistence;

namespace Oee.Infrastructure.Operations;

/// <summary>
/// Query service dựng toàn bộ Dashboard OEE từ snapshot, output, target, downtime.
/// </summary>
/// <remarks>
/// Filter theo cây tổ chức được áp dụng tại SQL. Dữ liệu tuyệt đối được đưa vào
/// DashboardRollupCalculator để A/P/Q/OEE của UI dùng cùng quy tắc Application; tỷ lệ trả về 0..1.
/// Khoảng DateOnly của người dùng được đổi sang UTC bằng timezone Site, không dùng timezone server.
/// </remarks>
public sealed partial class DashboardQueryService(
    OeeDbContext db,
    IClock clock,
    Oee.Application.Production.LineOff.ILineOffCalendarStore? offCalendar = null) : IDashboardQueryService
{
    // Giới hạn khoảng truy vấn để bảo vệ database và thời gian render biểu đồ.
    private const int MaximumRangeDays = 366;

    /// <summary>Đọc các lookup active và ngày mặc định theo timezone Site đầu tiên.</summary>
    public async Task<DashboardFilterOptions> GetFilterOptionsAsync(CancellationToken cancellationToken)
    {
        var sites = await db.Sites.AsNoTracking()
            .Where(site => site.Status == RecordStatusEnum.Active)
            .OrderBy(site => site.Code)
            .Select(site => new DashboardFilterOption(site.Id, site.Code, site.Name))
            .ToArrayAsync(cancellationToken);
        var areas = await db.Areas.AsNoTracking()
            .Where(area => area.Status == RecordStatusEnum.Active)
            .OrderBy(area => area.Code)
            .Select(area => new DashboardFilterOption(area.Id, area.Code, area.Name, area.SiteId))
            .ToArrayAsync(cancellationToken);
        var workCenters = await db.WorkCenters.AsNoTracking()
            .Where(group => group.Status == RecordStatusEnum.Active)
            .OrderBy(group => group.Code)
            .Select(group => new DashboardFilterOption(group.Id, group.Code, group.Name, group.AreaId))
            .ToArrayAsync(cancellationToken);
        var workUnits = await db.WorkUnits.AsNoTracking()
            .Where(workUnit => workUnit.Status == RecordStatusEnum.Active)
            .OrderBy(workUnit => workUnit.Code)
            .Select(workUnit => new DashboardFilterOption(workUnit.Id, workUnit.Code, workUnit.Name, workUnit.WorkCenterId))
            .ToArrayAsync(cancellationToken);
        var products = await db.Products.AsNoTracking()
            .Where(product => product.Status == RecordStatusEnum.Active)
            .OrderBy(product => product.Code)
            .Select(product => new DashboardFilterOption(product.Id, product.Code, product.Name))
            .ToArrayAsync(cancellationToken);
        var shifts = await db.Shifts.AsNoTracking()
            .Where(shift => shift.Status == RecordStatusEnum.Active)
            .OrderBy(shift => shift.StartTime)
            .Select(shift => new DashboardFilterOption(shift.Id, shift.Code, shift.Name, shift.SiteId))
            .ToArrayAsync(cancellationToken);

        var defaultTimeZoneId = await db.Sites.AsNoTracking()
            .Where(site => site.Status == RecordStatusEnum.Active)
            .OrderBy(site => site.Code)
            .Select(site => site.TimeZoneId)
            .FirstOrDefaultAsync(cancellationToken) ?? TimeZoneInfo.Utc.Id;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.UtcNow, FindTimeZone(defaultTimeZoneId)).DateTime);

        return new DashboardFilterOptions(today, sites, areas, workCenters, workUnits, products, shifts);
    }

    /// <summary>Tổng hợp KPI hiện tại, kỳ trước, trend và Pareto theo filter.</summary>
    public async Task<DashboardModel> GetAsync(DashboardQueryRequest request, CancellationToken cancellationToken)
    {
        Validate(request);

        // Dựng IQueryable scope theo Site/Area/Group/WorkUnit trước khi materialize.
        var scopeQuery = db.WorkUnits.AsNoTracking()
            .Where(workUnit => workUnit.Status == RecordStatusEnum.Active);
        if (request.SiteId.HasValue)
        {
            scopeQuery = scopeQuery.Where(workUnit => workUnit.WorkCenter.Area.SiteId == request.SiteId);
        }
        if (request.AreaId.HasValue)
        {
            scopeQuery = scopeQuery.Where(workUnit => workUnit.WorkCenter.AreaId == request.AreaId);
        }
        if (request.WorkCenterId.HasValue)
        {
            scopeQuery = scopeQuery.Where(workUnit => workUnit.WorkCenterId == request.WorkCenterId);
        }
        if (request.WorkUnitId.HasValue)
        {
            scopeQuery = scopeQuery.Where(workUnit => workUnit.Id == request.WorkUnitId);
        }

        var scope = await scopeQuery
            .OrderBy(workUnit => workUnit.Code)
            .Select(workUnit => new DashboardScopeRow(
                workUnit.Id,
                workUnit.WorkCenter.Area.SiteId,
                workUnit.WorkCenter.Area.Site.TimeZoneId))
            .ToArrayAsync(cancellationToken);

        // Một scope cùng Site dùng timezone đó; scope nhiều timezone dùng lựa chọn/fallback có kiểm soát.
        var timeZoneId = scope.Select(row => row.TimeZoneId).Distinct(StringComparer.Ordinal).Count() == 1
            ? scope[0].TimeZoneId
            : await ResolveRequestedTimeZoneAsync(request, cancellationToken);
        var timeZone = FindTimeZone(timeZoneId);
        var bounds = ToBounds(request.FromDate, request.ToDate, timeZone);
        var workUnitIds = scope.Select(row => row.Id).ToArray();
        var periodType = request.ShiftId.HasValue ? OeePeriodTypeEnum.Shift : OeePeriodTypeEnum.Day;
        var shiftCode = request.ShiftId.HasValue
            ? await db.Shifts.AsNoTracking().Where(shift => shift.Id == request.ShiftId).Select(shift => shift.Code).SingleOrDefaultAsync(cancellationToken)
            : null;

        if (scope.Length == 0 || request.ShiftId.HasValue && shiftCode is null)
        {
            return Empty(timeZoneId);
        }

        // Tải từng nhóm dữ liệu theo batch rồi rollup, không query trong vòng lặp WorkUnit.
        var currentSnapshots = await LoadSnapshotsAsync(workUnitIds, bounds.Start, bounds.End, periodType, shiftCode, cancellationToken);
        var currentProduction = await LoadProductionAsync(workUnitIds, bounds.Start, bounds.End, request.ProductId, request.ShiftId, cancellationToken);
        var current = DashboardRollupCalculator.Calculate(
            currentSnapshots.Select(row => new DashboardSnapshotInput(row.LoadingMinutes, row.UtilizationMinutes)),
            currentProduction.Select(row => new DashboardProductionInput(row.Total, row.Defect, row.IdealRunMinutes)));

        // Kỳ trước có cùng số ngày để Delta có ý nghĩa so sánh.
        var rangeDays = request.ToDate.DayNumber - request.FromDate.DayNumber + 1;
        var previousTo = request.FromDate.AddDays(-1);
        var previousFrom = previousTo.AddDays(-(rangeDays - 1));
        var previousBounds = ToBounds(previousFrom, previousTo, timeZone);
        var previousSnapshots = await LoadSnapshotsAsync(workUnitIds, previousBounds.Start, previousBounds.End, periodType, shiftCode, cancellationToken);
        var previousProduction = await LoadProductionAsync(workUnitIds, previousBounds.Start, previousBounds.End, request.ProductId, request.ShiftId, cancellationToken);
        var previous = DashboardRollupCalculator.Calculate(
            previousSnapshots.Select(row => new DashboardSnapshotInput(row.LoadingMinutes, row.UtilizationMinutes)),
            previousProduction.Select(row => new DashboardProductionInput(row.Total, row.Defect, row.IdealRunMinutes)));

        var siteId = scope.Select(row => row.SiteId).Distinct().Take(2).ToArray() is [var singleSite] ? singleSite : (Guid?)null;
        var target = await LoadTargetAsync(request.WorkUnitId, siteId, periodType, request.ToDate, cancellationToken);
        var productionTarget = await LoadProductionTargetAsync(workUnitIds, bounds.Start, bounds.End, request.ProductId, request.ShiftId, cancellationToken);
        var trend = BuildTrend(currentSnapshots, currentProduction, timeZone);
        var losses = await LoadLossesAsync(workUnitIds, bounds.Start, bounds.End, request.ProductId, request.ShiftId, cancellationToken);

        return new DashboardModel(
            Metric(current.Oee, target?.Oee, previous.Oee),
            Metric(current.Availability, target?.Availability, previous.Availability),
            Metric(current.Performance, target?.Performance, previous.Performance),
            Metric(current.Quality, target?.Quality, previous.Quality),
            new DashboardProductionTotals(current.TotalQuantity, current.GoodQuantity, current.RejectQuantity, productionTarget),
            new DashboardTimeTotals(current.LoadingMinutes, current.UtilizationMinutes, current.IdealRunMinutes),
            trend,
            losses,
            timeZoneId,
            currentSnapshots.Length > 0 || currentProduction.Length > 0);
    }

    /// <summary>Lấy snapshot giao với window và chọn bản tính mới nhất của mỗi period instance.</summary>
    private async Task<DashboardSnapshotRow[]> LoadSnapshotsAsync(
        IReadOnlyCollection<Guid> workUnitIds,
        DateTimeOffset from,
        DateTimeOffset until,
        OeePeriodTypeEnum periodType,
        string? shiftCode,
        CancellationToken cancellationToken)
    {
        var query = db.OeeSnapshots.AsNoTracking()
            .Where(snapshot => workUnitIds.Contains(snapshot.WorkUnitId)
                && snapshot.PeriodType == periodType
                && snapshot.PeriodStart < until
                && snapshot.PeriodEnd > from);
        if (!string.IsNullOrWhiteSpace(shiftCode))
        {
            var prefix = $"SHIFT-{shiftCode}-";
            query = query.Where(snapshot => snapshot.PeriodCode.StartsWith(prefix));
        }

        var rows = await query.Select(snapshot => new DashboardSnapshotRow(
                snapshot.WorkUnitId,
                snapshot.PeriodCode,
                snapshot.PeriodStart,
                snapshot.PeriodEnd,
                snapshot.LoadingMinutes,
                snapshot.UtilizationMinutes,
                snapshot.CalculatedAt,
                snapshot.CalculationVersion))
            .ToArrayAsync(cancellationToken);

        // Group sau projection để xử lý dữ liệu có nhiều CalculationVersion mà không thay đổi schema.
        return rows
            .GroupBy(row => new { row.WorkUnitId, row.PeriodCode, row.PeriodStart, row.PeriodEnd })
            .Select(group => group.OrderByDescending(row => row.CalculatedAt).ThenByDescending(row => row.CalculationVersion, StringComparer.Ordinal).First())
            .ToArray();
    }

    /// <summary>Lấy quantity và ideal run minutes của output trong phạm vi filter.</summary>
    private async Task<DashboardProductionRow[]> LoadProductionAsync(
        IReadOnlyCollection<Guid> workUnitIds,
        DateTimeOffset from,
        DateTimeOffset until,
        Guid? productId,
        Guid? shiftId,
        CancellationToken cancellationToken)
    {
        var query = db.ProductionOutputs.AsNoTracking()
            .Where(output => workUnitIds.Contains(output.WorkUnitId)
                && output.PeriodStart < until
                && output.PeriodEnd > from);
        if (productId.HasValue)
        {
            query = query.Where(output => output.ProductId == productId);
        }
        if (shiftId.HasValue)
        {
            query = query.Where(output => output.ShiftId == shiftId);
        }

        return await query.Select(output => new DashboardProductionRow(
                output.WorkUnitId,
                output.PeriodStart,
                output.TotalProducedQuantity,
                output.DefectQuantity,
                output.TotalProducedQuantity / output.StandardSpeedPerMinute))
            .ToArrayAsync(cancellationToken);
    }

    /// <summary>Chọn target hiệu lực, ưu tiên WorkUnit cụ thể hơn Site.</summary>
    private async Task<DashboardTargetRow?> LoadTargetAsync(
        Guid? workUnitId,
        Guid? siteId,
        OeePeriodTypeEnum periodType,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        if (!siteId.HasValue)
        {
            return null;
        }

        var query = db.OeeTargets.AsNoTracking()
            .Where(target => target.PeriodType == periodType
                && target.EffectiveFrom <= date
                && (!target.EffectiveTo.HasValue || target.EffectiveTo >= date)
                && (target.WorkUnitId == workUnitId
                    || target.WorkUnitId == null && target.SiteId == siteId));
        if (!workUnitId.HasValue)
        {
            query = query.Where(target => target.WorkUnitId == null);
        }

        return await query
            .OrderByDescending(target => target.WorkUnitId.HasValue)
            .ThenByDescending(target => target.EffectiveFrom)
            .Select(target => new DashboardTargetRow(
                target.MinimumOee,
                target.MinimumAvailability,
                target.MinimumPerformance,
                target.MinimumQuality))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<decimal> LoadProductionTargetAsync(
        IReadOnlyCollection<Guid> workUnitIds,
        DateTimeOffset from,
        DateTimeOffset until,
        Guid? productId,
        Guid? shiftId,
        CancellationToken cancellationToken)
    {
        var query = db.CurrentProductionPlans().AsNoTracking()
            .Where(plan => workUnitIds.Contains(plan.WorkUnitId)
                && plan.PlannedStart < until
                && plan.PlannedEnd > from);
        if (productId.HasValue)
        {
            query = query.Where(plan => plan.ProductId == productId);
        }
        if (shiftId.HasValue)
        {
            query = query.Where(plan => plan.ShiftId == shiftId);
        }

        return await query.Select(plan => (decimal?)plan.PlannedQuantity).SumAsync(cancellationToken) ?? 0;
    }

    /// <summary>Nhóm theo ngày địa phương và rollup KPI cho từng điểm trend.</summary>
    private static TrendPoint[] BuildTrend(
        IEnumerable<DashboardSnapshotRow> snapshots,
        IEnumerable<DashboardProductionRow> production,
        TimeZoneInfo timeZone)
    {
        var snapshotGroups = snapshots.GroupBy(row => LocalDate(row.PeriodStart, timeZone))
            .ToDictionary(group => group.Key, group => group.ToArray());
        var productionGroups = production.GroupBy(row => LocalDate(row.PeriodStart, timeZone))
            .ToDictionary(group => group.Key, group => group.ToArray());

        return snapshotGroups.Keys.Union(productionGroups.Keys).OrderBy(date => date).Select(date =>
        {
            var snapshotRows = snapshotGroups.GetValueOrDefault(date) ?? [];
            var productionRows = productionGroups.GetValueOrDefault(date) ?? [];
            var rollup = DashboardRollupCalculator.Calculate(
                snapshotRows.Select(row => new DashboardSnapshotInput(row.LoadingMinutes, row.UtilizationMinutes)),
                productionRows.Select(row => new DashboardProductionInput(row.Total, row.Defect, row.IdealRunMinutes)));
            return new TrendPoint(date, rollup.Oee, rollup.Availability, rollup.Performance, rollup.Quality);
        }).ToArray();
    }

    /// <summary>Tính top 5 downtime Pareto sau khi clip từng event vào window.</summary>
    private async Task<LossPoint[]> LoadLossesAsync(
        IReadOnlyCollection<Guid> workUnitIds,
        DateTimeOffset from,
        DateTimeOffset until,
        Guid? productId,
        Guid? shiftId,
        CancellationToken cancellationToken)
    {
        var query = db.DowntimeEvents.AsNoTracking()
            .Where(item => workUnitIds.Contains(item.WorkUnitId)
                && item.StartAt < until
                && (item.EndAt == null || item.EndAt > from));
        if (productId.HasValue)
        {
            query = query.Where(item => item.ProductionOutput != null && item.ProductionOutput.ProductId == productId);
        }
        if (shiftId.HasValue)
        {
            query = query.Where(item => item.ProductionOutput != null && item.ProductionOutput.ShiftId == shiftId);
        }

        var events = await query.Select(item => new DashboardLossEventRow(
                item.DowntimeReason.Code,
                item.DowntimeReason.Name,
                item.Category.ToString(),
                item.StartAt,
                item.EndAt))
            .ToArrayAsync(cancellationToken);
        var grouped = events.GroupBy(item => new { item.ReasonCode, item.Reason, item.Category })
            .Select(group => new
            {
                group.Key,
                Count = group.Count(),
                Minutes = group.Sum(item => DurationMinutes(item.StartAt, item.EndAt ?? until, from, until)),
            })
            .Where(item => item.Minutes > 0)
            .OrderByDescending(item => item.Minutes)
            .ThenBy(item => item.Key.ReasonCode, StringComparer.Ordinal)
            .Take(5)
            .ToArray();
        var total = grouped.Sum(item => item.Minutes);
        return grouped.Select(item => new LossPoint(
            item.Key.ReasonCode,
            item.Key.Reason,
            item.Key.Category,
            item.Count,
            item.Minutes,
            total == 0 ? 0 : item.Minutes / total)).ToArray();
    }

    private async Task<string> ResolveRequestedTimeZoneAsync(DashboardQueryRequest request, CancellationToken cancellationToken)
    {
        if (request.SiteId.HasValue)
        {
            return await db.Sites.AsNoTracking().Where(site => site.Id == request.SiteId).Select(site => site.TimeZoneId).SingleOrDefaultAsync(cancellationToken)
                ?? TimeZoneInfo.Utc.Id;
        }

        return await db.Sites.AsNoTracking().OrderBy(site => site.Code).Select(site => site.TimeZoneId).FirstOrDefaultAsync(cancellationToken)
            ?? TimeZoneInfo.Utc.Id;
    }

    private static DashboardRatioMetric Metric(decimal? value, decimal? target, decimal? previous) =>
        new(value, target, previous, value.HasValue && previous.HasValue ? value.Value - previous.Value : null);

    private static DashboardModel Empty(string timeZoneId) => new(
        new(null, null, null, null),
        new(null, null, null, null),
        new(null, null, null, null),
        new(null, null, null, null),
        new(0, 0, 0, 0),
        new(0, 0, 0),
        [],
        [],
        timeZoneId,
        false);

    private static void Validate(DashboardQueryRequest request)
    {
        if (request.ToDate < request.FromDate)
        {
            throw new ArgumentException("Ngày kết thúc phải từ ngày bắt đầu trở đi.", nameof(request));
        }
        if (request.ToDate.DayNumber - request.FromDate.DayNumber + 1 > MaximumRangeDays)
        {
            throw new ArgumentException($"Dashboard chỉ cho phép tối đa {MaximumRangeDays} ngày mỗi truy vấn.", nameof(request));
        }
    }

    /// <summary>Đổi khoảng ngày inclusive của Site thành [UTC start, UTC end-exclusive).</summary>
    private static (DateTimeOffset Start, DateTimeOffset End) ToBounds(DateOnly from, DateOnly to, TimeZoneInfo timeZone)
    {
        var start = LocalMidnight(from, timeZone);
        var end = LocalMidnight(to.AddDays(1), timeZone);
        return (start, end);
    }

    /// <summary>Resolve nửa đêm local và từ chối thời điểm DST không tồn tại.</summary>
    private static DateTimeOffset LocalMidnight(DateOnly date, TimeZoneInfo timeZone)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        if (timeZone.IsInvalidTime(local))
        {
            throw new ArgumentException($"Ngày {date:yyyy-MM-dd} bắt đầu trong khoảng thời gian DST không hợp lệ.");
        }
        return new DateTimeOffset(local, timeZone.GetUtcOffset(local)).ToUniversalTime();
    }

    private static DateOnly LocalDate(DateTimeOffset value, TimeZoneInfo timeZone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(value, timeZone).DateTime);

    /// <summary>Clip event vào filter window rồi trả duration phút không âm.</summary>
    private static decimal DurationMinutes(DateTimeOffset start, DateTimeOffset end, DateTimeOffset from, DateTimeOffset until)
    {
        var clippedStart = start < from ? from : start;
        var clippedEnd = end > until ? until : end;
        return clippedEnd <= clippedStart ? 0 : (decimal)(clippedEnd - clippedStart).TotalMinutes;
    }

    private static TimeZoneInfo FindTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (TimeZoneNotFoundException exception)
        {
            throw new InvalidOperationException($"Múi giờ site '{timeZoneId}' không tồn tại trên host.", exception);
        }
        catch (InvalidTimeZoneException exception)
        {
            throw new InvalidOperationException($"Múi giờ site '{timeZoneId}' không hợp lệ.", exception);
        }
    }
}
