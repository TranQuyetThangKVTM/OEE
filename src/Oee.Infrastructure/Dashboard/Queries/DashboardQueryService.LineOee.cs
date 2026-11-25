using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.Reporting;
using Oee.Application.Modules;
using Oee.Application.Production.LineOff;
using Oee.Domain.Common;
using Oee.Domain.Entities.Production;
using Oee.Domain.Oee;
using Oee.Infrastructure.Reporting;
using Oee.Infrastructure.Persistence;

namespace Oee.Infrastructure.Operations;

public sealed partial class DashboardQueryService
{
    public async Task<LineOeeSummary> GetLineOeeAsync(DashboardQueryRequest request, CancellationToken cancellationToken)
    {
        Validate(request);
        var machines = db.WorkUnits.AsNoTracking()
            .Where(unit => unit.Status == RecordStatusEnum.Active && unit.Name.Trim() == "Filler"
                && unit.WorkCenter.Status == RecordStatusEnum.Active && unit.WorkCenter.Area.Status == RecordStatusEnum.Active
                && unit.WorkCenter.Area.Site.Status == RecordStatusEnum.Active);
        if (request.SiteId.HasValue) machines = machines.Where(unit => unit.WorkCenter.Area.SiteId == request.SiteId);
        if (request.AreaId.HasValue) machines = machines.Where(unit => unit.WorkCenter.AreaId == request.AreaId);
        if (request.WorkCenterId.HasValue) machines = machines.Where(unit => unit.WorkCenterId == request.WorkCenterId);
        var candidates = await machines.Select(unit => new
        {
            unit.Id, unit.Code, unit.Name, unit.WorkCenterId, unit.WorkCenter.Area.SiteId,
            Line = unit.WorkCenter.Code + " · " + unit.WorkCenter.Name,
            unit.WorkCenter.Area.Site.TimeZoneId
        }).Take(2).ToArrayAsync(cancellationToken);
        if (candidates.Length != 1)
            return new(request.ToDate, null, null, null, null, null, null,
                candidates.Length == 0 ? "Không tìm thấy máy Filler đang hoạt động trong phạm vi đã chọn."
                    : "Có nhiều máy Filler. Hãy chọn một dây chuyền để xem OEE Line.");

        var filler = candidates[0];
        var zone = FindTimeZone(filler.TimeZoneId);
        var machine = filler.Code + " · " + filler.Name;
        var now = clock.UtcNow;
        if (request.ToDate > LocalDate(now, zone))
            return new(request.ToDate, filler.Line, machine, filler.TimeZoneId, null, null, null,
                "Ngày đã chọn chưa đến, chưa có dữ liệu OEE Line.", filler.WorkCenterId);
        var bounds = ToBounds(new(request.ToDate.Year, request.ToDate.Month, 1), request.ToDate, zone);
        var dayStart = ToBounds(request.ToDate, request.ToDate, zone).Start;
        var end = bounds.End < now ? bounds.End : now;
        var lastData = await db.PlcDeviceRuntimeStates.AsNoTracking()
            .Where(x => x.PlcDeviceMapping.WorkUnitId == filler.Id)
            .Select(x => x.LastDataAt).MaxAsync(cancellationToken);
        // Không tính vài giây đang chờ mẫu OPC tiếp theo thành mất dữ liệu.
        if (lastData.HasValue && lastData <= end && end - lastData <= TimeSpan.FromSeconds(30)) end = lastData.Value;
        if (end <= bounds.Start)
            return new(request.ToDate, filler.Line, machine, filler.TimeZoneId, null, null, now,
                "Chưa có thời gian sản xuất trong kỳ đã chọn.", filler.WorkCenterId);

        var plans = await db.CurrentProductionPlans().AsNoTracking()
            .Where(x => x.WorkUnitId == filler.Id && x.PlannedStart < end && x.PlannedEnd > bounds.Start)
            .Select(x => new { x.PlannedStart, x.PlannedEnd }).ToArrayAsync(cancellationToken);
        var planned = OeeTimeWindowValue.Merge(plans.Select(x => OeeTimeWindowValue.Create(
            x.PlannedStart < bounds.Start ? bounds.Start : x.PlannedStart, x.PlannedEnd > end ? end : x.PlannedEnd)));
        var calendar = offCalendar is null ? LineOffCalendar.Empty : await offCalendar.ReadAsync(cancellationToken);
        var off = calendar.Periods.Where(x => !x.Deleted && x.WorkCenterId == filler.WorkCenterId && x.Start < end && x.End > bounds.Start)
            .Select(x => OeeTimeWindowValue.Create(x.Start, x.End)).ToArray();
        var operating = OeeTimeWindowValue.Exclude(planned, off);
        var source = db.ProductionOutputs.AsNoTracking()
            .Where(x => x.WorkUnitId == filler.Id && x.PeriodEnd > bounds.Start && x.PeriodEnd <= end);
        if (off.Length > 0 && await FilterWindows(source, off).AnyAsync(x => x.TotalProducedQuantity > 0, cancellationToken))
            return new(request.ToDate, filler.Line, machine, filler.TimeZoneId, null, null, now,
                "Filler có sản lượng trong khoảng đã đánh dấu Off. Hãy kiểm tra/sửa lịch Off trước khi tính OEE.", filler.WorkCenterId);
        var production = await FilterWindows(source, operating)
            .GroupBy(x => x.PeriodEnd > dayStart)
            .Select(group => new
            {
                IsDay = group.Key, Total = group.Sum(x => x.TotalProducedQuantity), Defect = group.Sum(x => x.DefectQuantity),
                Ideal = group.Sum(x => x.TotalProducedQuantity / x.StandardSpeedPerMinute),
                Unscheduled = group.Sum(x => x.UnscheduledMinutes)
            }).ToArrayAsync(cancellationToken);
        var stops = await db.DowntimeEvents.AsNoTracking()
            .Where(x => x.WorkUnitId == filler.Id && x.StartAt < end && (x.EndAt == null || x.EndAt > bounds.Start))
            .Select(x => new OeeDowntimeInputValue(x.StartAt, x.EndAt ?? end, x.Category, x.Kind)).ToArrayAsync(cancellationToken);

        var timeline = await new ReportTimelineQuery(db, clock).LoadAsync(
            new(ReportKind.DeviceOee, new(request.ToDate.Year, request.ToDate.Month, 1), request.ToDate, WorkUnitId: filler.Id),
            [new(filler.Id, filler.Code, filler.Name, filler.SiteId, filler.TimeZoneId)], new(bounds.Start, end), zone, cancellationToken);
        var unknown = timeline.Rows.SelectMany(x => x.Segments)
            .Where(x => x.State is EquipmentTimelineState.Unknown or EquipmentTimelineState.NoProduction)
            .Select(x => OeeTimeWindowValue.Create(x.Start, x.End))
            .Concat(await LoadCollectionGapsAsync(filler.Id, bounds.Start, end, cancellationToken)).ToArray();
        var monthMissing = OverlapMinutes(operating, unknown, bounds.Start, end);
        var dayMissing = OverlapMinutes(operating, unknown, dayStart, end);
        var dayProduction = production.Where(x => x.IsDay).ToArray();
        var unallocated = await db.ProductionReconciliationItems.AsNoTracking()
            .Where(x => x.WorkUnitId == filler.Id && x.CapturedAt > bounds.Start && x.CapturedAt <= end
                && x.ReasonCode == "CROSS_PLAN_COLLECTION_GAP" && x.State == ReconciliationStateEnum.Pending)
            .Select(x => x.CapturedAt).ToArrayAsync(cancellationToken);
        var dayUnallocated = unallocated.Any(x => LocalDate(x, zone) == request.ToDate);
        var day = end <= dayStart ? null : LineOeeCalculator.Calculate(dayStart, end, operating,
            new(dayProduction.Sum(x => x.Total), dayProduction.Sum(x => x.Defect), dayProduction.Sum(x => x.Ideal)), stops,
            dayProduction.Sum(x => x.Unscheduled));
        var month = LineOeeCalculator.Calculate(bounds.Start, end, operating,
            new(production.Sum(x => x.Total), production.Sum(x => x.Defect), production.Sum(x => x.Ideal)), stops,
            production.Sum(x => x.Unscheduled));
        return new(request.ToDate, filler.Line, machine, filler.TimeZoneId,
            dayMissing > 0 || dayUnallocated ? null : day, monthMissing > 0 || unallocated.Length > 0 ? null : month, now,
            unallocated.Length > 0 ? "Có sản lượng Filler chưa xác định được ca sau gián đoạn thu thập. OEE tháng chưa đủ dữ liệu để chốt."
                : monthMissing > 0 ? "Còn thời gian phải sản xuất nhưng chưa đủ dữ liệu Filler. Không tự coi khoảng này là Off hoặc máy dừng." : null,
            filler.WorkCenterId,
            OverlapMinutes(planned, off, dayStart, end), OverlapMinutes(planned, off, bounds.Start, end), dayMissing, monthMissing);
    }

    private static IQueryable<ProductionOutputEntity> FilterWindows(
        IQueryable<ProductionOutputEntity> source, IReadOnlyCollection<OeeTimeWindowValue> windows)
    {
        var parameter = Expression.Parameter(typeof(ProductionOutputEntity), "output");
        var timestamp = Expression.Property(parameter, nameof(ProductionOutputEntity.PeriodEnd));
        Expression body = Expression.Constant(false);
        foreach (var window in windows)
            body = Expression.OrElse(body, Expression.AndAlso(
                Expression.GreaterThan(timestamp, Expression.Constant(window.Start)),
                Expression.LessThanOrEqual(timestamp, Expression.Constant(window.End))));
        return source.Where(Expression.Lambda<Func<ProductionOutputEntity, bool>>(body, parameter));
    }

    private static decimal OverlapMinutes(IEnumerable<OeeTimeWindowValue> first, IEnumerable<OeeTimeWindowValue> second,
        DateTimeOffset start, DateTimeOffset end)
        => OeeTimeWindowValue.Merge(first).Sum(a => OeeTimeWindowValue.Merge(second).Sum(b =>
        {
            var from = new[] { a.Start, b.Start, start }.Max();
            var until = new[] { a.End, b.End, end }.Min();
            return until > from ? (decimal)(until - from).TotalMinutes : 0;
        }));

    private async Task<OeeTimeWindowValue[]> LoadCollectionGapsAsync(Guid unitId, DateTimeOffset start,
        DateTimeOffset until, CancellationToken cancellationToken)
    {
        var source = db.PlcSessionEvents.AsNoTracking().Where(x => x.OccurredAt < until
            && db.PlcDeviceMappings.Any(mapping => mapping.WorkUnitId == unitId && mapping.IsActive
                && mapping.OpcUaConnectionId == x.OpcUaConnectionId));
        var previous = await source.Where(x => x.OccurredAt < start).GroupBy(x => x.OpcUaConnectionId)
            .Select(group => group.OrderByDescending(x => x.OccurredAt)
                .Select(x => new { x.OpcUaConnectionId, x.OccurredAt, x.State }).First()).ToArrayAsync(cancellationToken);
        var current = await source.Where(x => x.OccurredAt >= start)
            .Select(x => new { x.OpcUaConnectionId, x.OccurredAt, x.State }).ToArrayAsync(cancellationToken);
        var gaps = new List<OeeTimeWindowValue>();
        foreach (var group in previous.Concat(current).GroupBy(x => x.OpcUaConnectionId))
        {
            DateTimeOffset? opened = null;
            foreach (var item in group.OrderBy(x => x.OccurredAt))
            {
                if (item.State != ConnectivityStateEnum.Connected) opened ??= item.OccurredAt < start ? start : item.OccurredAt;
                else if (opened is { } at)
                {
                    if (item.OccurredAt > at) gaps.Add(OeeTimeWindowValue.Create(at, item.OccurredAt));
                    opened = null;
                }
            }
            if (opened is { } pending && until > pending) gaps.Add(OeeTimeWindowValue.Create(pending, until));
        }
        var blocked = await db.CounterCheckpoints.AsNoTracking()
            .Where(x => x.PlcDeviceMapping.WorkUnitId == unitId && x.LastReadAt < until
                && (x.LastSampleKey == "!counter-discontinuity" || x.LastSampleKey != null
                    && x.LastSampleKey.StartsWith(Oee.Domain.PlcIntegration.CounterCheckpointEntity.RecoveryPendingPrefix)))
            .Select(x => x.LastReadAt).ToArrayAsync(cancellationToken);
        foreach (var at in blocked.OfType<DateTimeOffset>())
            gaps.Add(OeeTimeWindowValue.Create(at < start ? start : at, until));
        return OeeTimeWindowValue.Merge(gaps);
    }
}
