using Microsoft.EntityFrameworkCore;
using Oee.Application.Common;
using Oee.Application.Contracts.Reporting;
using Oee.Application.Reporting;
using Oee.Domain.Common;
using Oee.Infrastructure.Persistence;
using static Oee.Infrastructure.Reporting.ReportTimeResolver;

namespace Oee.Infrastructure.Reporting;
/// <summary>Đọc và dựng dòng thời gian thiết bị từ plan/output/downtime và lịch sử PLC.</summary>
internal sealed class ReportTimelineQuery(OeeDbContext db, IClock clock)
{
    internal async Task<EquipmentTimelineData> LoadAsync(ReportQuery query, ReportScopeRow[] scope, ReportTimeBounds bounds, TimeZoneInfo zone, CancellationToken ct)
    {
        var ids = scope.Select(x => x.Id).ToArray();
        var plansQuery = db.CurrentProductionPlans().AsNoTracking().Where(x => ids.Contains(x.WorkUnitId) && x.PlannedStart < bounds.End && x.PlannedEnd > bounds.Start);
        var outputsQuery = db.ProductionOutputs.AsNoTracking().Where(x => ids.Contains(x.WorkUnitId) && x.TotalProducedQuantity > 0 && x.PeriodStart < bounds.End && x.PeriodEnd > bounds.Start);
        var stopsQuery = db.DowntimeEvents.AsNoTracking().Where(x => ids.Contains(x.WorkUnitId) && x.StartAt < bounds.End && (x.EndAt == null || x.EndAt > bounds.Start));
        if (query.ProductId.HasValue)
        {
            plansQuery = plansQuery.Where(x => x.ProductId == query.ProductId);
            outputsQuery = outputsQuery.Where(x => x.ProductId == query.ProductId);
            stopsQuery = stopsQuery.Where(x => x.ProductionOutput != null && x.ProductionOutput.ProductId == query.ProductId);
        }

        if (query.ShiftId.HasValue)
        {
            plansQuery = plansQuery.Where(x => x.ShiftId == query.ShiftId);
            outputsQuery = outputsQuery.Where(x => x.ShiftId == query.ShiftId);
            stopsQuery = stopsQuery.Where(x => x.ProductionOutput == null || x.ProductionOutput.ShiftId == query.ShiftId);
        }

        var plans = await plansQuery.Select(x => new ReportTimelineInterval(x.WorkUnitId, x.PlannedStart, x.PlannedEnd)).ToArrayAsync(ct);
        var outputs = await outputsQuery.Select(x => new ReportTimelineInterval(x.WorkUnitId, x.PeriodStart, x.PeriodEnd)).ToArrayAsync(ct);
        var stops = await stopsQuery.Select(x => new ReportTimelineStop(x.Id, x.WorkUnitId, x.StartAt, x.EndAt, x.Kind, x.Category, x.DowntimeReason.Name, x.Note)).ToArrayAsync(ct);
        var windows = await TimelineWindows(query, bounds, zone, ct);
        var history = query.ProductId.HasValue ? new ReportTimelineHistory([], []) : await LoadStateHistory(ids, bounds, ct);
        var rows = new List<EquipmentTimelineRow>();
        var now = clock.UtcNow;
        foreach (var unit in scope)
        {
            ct.ThrowIfCancellationRequested();
            if (!plans.Any(x => x.UnitId == unit.Id) && !outputs.Any(x => x.UnitId == unit.Id) && !stops.Any(x => x.UnitId == unit.Id) && !history.Units.Contains(unit.Id))
                continue;
            var spans = plans.Where(x => x.UnitId == unit.Id).Select(x => new EquipmentTimelineSpan(x.Start, x.End, EquipmentTimelineState.Unknown)).Concat(outputs.Where(x => x.UnitId == unit.Id && !history.Units.Contains(unit.Id)).Select(x => new EquipmentTimelineSpan(x.Start, x.End, EquipmentTimelineState.Running))).Concat(history.Spans.Where(x => x.UnitId == unit.Id).Select(x => x.Span)).Concat(stops.Where(x => x.UnitId == unit.Id).Select(x => new EquipmentTimelineSpan(x.Start, x.End ?? now, StopState(x.Kind, x.Category), x.Id, x.Reason, x.Note, x.End == null)));
            var clipped = spans.SelectMany(span => windows.Select(window => span with { Start = span.Start < window.Start ? window.Start : span.Start, End = span.End > window.End ? window.End : span.End })).Where(x => x.End > x.Start);
            rows.Add(EquipmentTimelineBuilder.Build(unit.Id, unit.Code, unit.Name, bounds.Start, bounds.End, now, clipped));
        }

        return new EquipmentTimelineData(bounds.Start, bounds.End, now < bounds.End ? now : bounds.End, zone.Id, rows);
    }

    private async Task<ReportTimelineHistory> LoadStateHistory(Guid[] unitIds, ReportTimeBounds bounds, CancellationToken ct)
    {
        var source = db.PlcIntegrationEvents.AsNoTracking().Where(x => x.PlcDeviceMapping != null && unitIds.Contains(x.PlcDeviceMapping.WorkUnitId) && (x.Kind == "StateTransition" || x.Kind == "Connectivity" && x.Message != "Connected") && x.OccurredAt < bounds.End);
        var prior = await source.Where(x => x.OccurredAt < bounds.Start).GroupBy(x => x.PlcDeviceMappingId).Select(group => group.OrderByDescending(x => x.OccurredAt).Select(x => new ReportTimelineStateEvent(x.PlcDeviceMappingId!.Value, x.PlcDeviceMapping!.WorkUnitId, x.OccurredAt, x.Kind, x.Message)).First()).ToArrayAsync(ct);
        var current = await source.Where(x => x.OccurredAt >= bounds.Start).Select(x => new ReportTimelineStateEvent(x.PlcDeviceMappingId!.Value, x.PlcDeviceMapping!.WorkUnitId, x.OccurredAt, x.Kind, x.Message)).ToArrayAsync(ct);
        var runtimes = await db.PlcDeviceRuntimeStates.AsNoTracking().Where(x => unitIds.Contains(x.PlcDeviceMapping.WorkUnitId)).Select(x => new { x.PlcDeviceMappingId, x.LastDataAt }).ToArrayAsync(ct);
        var spans = new List<ReportTimelineHistorySpan>();
        var units = new HashSet<Guid>();
        foreach (var group in prior.Concat(current).GroupBy(x => x.MappingId))
        {
            var events = group.OrderBy(x => x.At).ToArray();
            var lastData = runtimes.FirstOrDefault(x => x.PlcDeviceMappingId == group.Key)?.LastDataAt;
            DateTimeOffset? begin = null;
            EquipmentTimelineState? state = null;
            foreach (var item in events)
            {
                if (begin.HasValue && state.HasValue && item.At > begin)
                    spans.Add(new(item.UnitId, new(begin.Value, item.At, state.Value, Reason: "Trạng thái PLC")));
                begin = null;
                state = null;
                if (item.Kind != "StateTransition")
                    continue;
                units.Add(item.UnitId);
                state = item.Message.EndsWith(" -> Running", StringComparison.Ordinal) ? EquipmentTimelineState.Running : item.Message.EndsWith(" -> Stopped", StringComparison.Ordinal) ? EquipmentTimelineState.Stopped : null;
                if (state.HasValue)
                    begin = item.At;
            }

            if (begin.HasValue && state.HasValue && lastData.HasValue && lastData.Value > begin.Value)
                spans.Add(new(events[^1].UnitId, new(begin.Value, lastData.Value, state.Value, Reason: "Trạng thái PLC")));
        }

        return new ReportTimelineHistory(spans, units);
    }

    internal async Task<ReportTimeBounds[]> TimelineWindows(ReportQuery query, ReportTimeBounds bounds, TimeZoneInfo zone, CancellationToken ct)
    {
        if (!query.ShiftId.HasValue)
            return[bounds];
        var shift = await db.Shifts.AsNoTracking().Where(x => x.Id == query.ShiftId).Select(x => new ReportTimelineShift(x.SiteId, x.StartTime, x.EndTime, x.Site.TimeZoneId)).SingleOrDefaultAsync(ct);
        if (shift is null)
            return[];
        zone = FindZone(shift.TimeZoneId);
        return Enumerable.Range(0, query.ToDate.DayNumber - query.FromDate.DayNumber + 1).Select(index => query.FromDate.AddDays(index)).Select(date => new ReportTimeBounds(LocalBoundary(date, shift.Start, zone), LocalBoundary(date.AddDays(shift.End <= shift.Start ? 1 : 0), shift.End, zone))).ToArray();
    }

    private static EquipmentTimelineState StopState(DowntimeKindEnum kind, DowntimeCategoryEnum category) => kind switch
    {
        DowntimeKindEnum.Failure or DowntimeKindEnum.Breakdown => EquipmentTimelineState.Failure,
        DowntimeKindEnum.Planned => EquipmentTimelineState.PlannedStop,
        _ => category is DowntimeCategoryEnum.PerformanceLoss or DowntimeCategoryEnum.QualityLoss ? EquipmentTimelineState.OtherStop : EquipmentTimelineState.Stopped
    };
}
