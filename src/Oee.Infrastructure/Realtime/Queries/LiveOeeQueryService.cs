using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Oee.Application.Common;
using Oee.Application.Contracts.Modules;
using Oee.Application.Modules;
using Oee.Domain.Common;
using Oee.Domain.Organization;
using Oee.Domain.Production;
using Oee.Domain.PlcIntegration;
using Oee.Infrastructure.Persistence;

namespace Oee.Infrastructure.Operations;

/// <summary>
/// Tổng hợp dữ liệu Live OEE từ runtime PLC, snapshot, active plan, output và downtime mở.
/// </summary>
/// <remarks>
/// Service thực hiện các query theo tập Id rồi ghép bằng dictionary để tránh N+1. ConnectivityState
/// và OperationalState luôn tách biệt: Disconnected/NoData không tự biến máy thành Stopped.
/// DTO trả SourceTimestamp để UI biết độ mới của dữ liệu.
/// </remarks>
public sealed class LiveOeeQueryService(OeeDbContext db, IClock clock) : ILiveOeeQueryService
{
    /// <summary>Đọc snapshot realtime hiện tại của toàn fleet theo ngưỡng stale cấu hình.</summary>
    public async Task<LiveOeeModel> GetAsync(LiveOeeQuery query, CancellationToken ct)
    {
        // Clamp ngưỡng stale tránh cấu hình client quá nhỏ gây false alarm hoặc quá lớn che NoData.
        var now = clock.UtcNow;
        var staleAfter = TimeSpan.FromSeconds(Math.Clamp(query.StaleAfterSeconds, 20, 7200));
        var units = await db.WorkUnits.AsNoTracking().OrderBy(unit => unit.Code).Select(unit => new LiveOeeUnitRow(
            unit.Id, unit.Code, unit.Name, unit.OperationalState,
            unit.WorkCenter.Area.SiteId, unit.WorkCenter.Area.Site.TimeZoneId)).ToArrayAsync(ct);
        if (units.Length == 0) return Empty(now);
        var unitIds = units.Select(unit => unit.Id).ToArray();
        var periodsBySite = new Dictionary<Guid, LiveOeePeriodBounds>();
        foreach (var site in units.DistinctBy(unit => unit.SiteId))
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(site.TimeZoneId);
            var date = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
            var weekStart = date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
            var monthStart = new DateOnly(date.Year, date.Month, 1);
            var siteShift = await ResolveShiftAsync(site.SiteId, site.TimeZoneId, now, ct);
            periodsBySite.Add(site.SiteId, new(date, ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue)),
                Midnight(date, zone), Midnight(weekStart, zone), Midnight(monthStart, zone), siteShift));
        }
        var periodOutputs = await LoadPeriodOutputsAsync(units, periodsBySite, now, ct);
        var gapSince = periodsBySite.Values.Min(period => period.MonthStart);
        var collectionGaps = await db.ProductionReconciliationItems.AsNoTracking()
            .Where(item => unitIds.Contains(item.WorkUnitId) && item.State == ReconciliationStateEnum.Pending
                && item.ReasonCode == ProductionOutputAttributionRules.CollectionGapReason
                && item.CapturedAt >= gapSince && item.CapturedAt <= now)
            .GroupBy(item => item.WorkUnitId)
            .Select(group => new { WorkUnitId = group.Key, Quantity = group.Sum(item => item.TotalDelta), At = group.Max(item => item.CapturedAt) })
            .ToDictionaryAsync(item => item.WorkUnitId, ct);

        // Runtime, checkpoint, snapshot, plan/output và downtime được tải theo batch riêng.
        var runtimes = await db.PlcDeviceRuntimeStates.AsNoTracking()
            .Where(runtime => unitIds.Contains(runtime.PlcDeviceMapping.WorkUnitId))
            .Select(runtime => new LiveOeeRuntimeRow(runtime.PlcDeviceMapping.WorkUnitId, runtime.PlcDeviceMappingId,
                runtime.ConnectivityState, runtime.OperationalState, runtime.LastDataAt, runtime.LastTransitionAt))
            .ToArrayAsync(ct);
        var runtimeByUnit = runtimes.GroupBy(runtime => runtime.WorkUnitId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.LastDataAt).First());
        var deviceIds = runtimes.Select(runtime => runtime.DeviceId).Distinct().ToArray();
        var checkpoints = await db.CounterCheckpoints.AsNoTracking().Where(checkpoint => deviceIds.Contains(checkpoint.PlcDeviceMappingId))
            .Select(checkpoint => new { checkpoint.PlcDeviceMappingId, checkpoint.LastTotalCount, checkpoint.LastCounterChangedAt, checkpoint.LastReadAt, Blocked = checkpoint.LastSampleKey == "!counter-discontinuity"
                || checkpoint.LastSampleKey != null && checkpoint.LastSampleKey.StartsWith(CounterCheckpointEntity.RecoveryPendingPrefix) })
            .ToDictionaryAsync(checkpoint => checkpoint.PlcDeviceMappingId, ct);

        // Subquery lấy CalculatedAt mới nhất, sau đó Join để EF trả đúng snapshot mới nhất mỗi WorkUnit.
        var latestTimes = db.OeeSnapshots.AsNoTracking().Where(snapshot => unitIds.Contains(snapshot.WorkUnitId)
                && snapshot.PeriodType == OeePeriodTypeEnum.Shift && snapshot.PeriodStart <= now && snapshot.PeriodEnd > now)
            .GroupBy(snapshot => snapshot.WorkUnitId)
            .Select(group => new { WorkUnitId = group.Key, CalculatedAt = group.Max(snapshot => snapshot.CalculatedAt) });
        var snapshotRows = await db.OeeSnapshots.AsNoTracking()
            .Where(snapshot => snapshot.PeriodType == OeePeriodTypeEnum.Shift && snapshot.PeriodStart <= now && snapshot.PeriodEnd > now)
            .Join(latestTimes, snapshot => new { snapshot.WorkUnitId, snapshot.CalculatedAt }, latest => new { latest.WorkUnitId, latest.CalculatedAt },
                (snapshot, _) => new LiveOeeSnapshotRow(snapshot.WorkUnitId, snapshot.Oee, snapshot.Availability,
                    snapshot.Performance, snapshot.Quality, snapshot.LoadingMinutes, snapshot.UtilizationMinutes,
                    snapshot.TotalQuantity, snapshot.DefectQuantity, snapshot.CalculatedAt, snapshot.AnomalyFlags))
            .ToArrayAsync(ct);
        var snapshotsByUnit = snapshotRows.GroupBy(snapshot => snapshot.WorkUnitId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.CalculatedAt).First());

        var plans = await db.CurrentProductionPlans().AsNoTracking()
            .Where(plan => unitIds.Contains(plan.WorkUnitId)
                && plan.PlannedStart <= now && plan.PlannedEnd > now)
            .OrderByDescending(plan => plan.Version)
            .Select(plan => new LiveOeePlanRow(plan.Id, plan.WorkUnitId, plan.Code, plan.ProductId, plan.Product.Code, plan.PlannedQuantity))
            .ToArrayAsync(ct);
        var planByUnit = plans.GroupBy(plan => plan.WorkUnitId).ToDictionary(group => group.Key, group => group.First());
        var planIds = plans.Select(plan => plan.Id).ToArray();
        var outputRows = await db.ProductionOutputs.AsNoTracking().Where(output => planIds.Contains(output.ProductionPlanId))
            .GroupBy(output => output.ProductionPlanId)
            .Select(group => new LiveOeeOutputRow(group.Key, group.Sum(output => output.TotalProducedQuantity), group.Sum(output => output.DefectQuantity)))
            .ToArrayAsync(ct);
        var outputByPlan = outputRows.ToDictionary(output => output.PlanId);

        var downtimeRows = await db.DowntimeEvents.AsNoTracking().Where(downtime => unitIds.Contains(downtime.WorkUnitId) && downtime.EndAt == null)
            .Select(downtime => new LiveOeeDowntimeRow(downtime.WorkUnitId, downtime.StartAt, downtime.DowntimeReason.Code, downtime.DowntimeReason.Name))
            .ToArrayAsync(ct);
        var downtimeByUnit = downtimeRows.GroupBy(downtime => downtime.WorkUnitId).ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.StartAt).First());
        var topReason = downtimeRows.GroupBy(downtime => new { downtime.ReasonCode, downtime.ReasonName })
            .OrderByDescending(group => group.Count()).ThenBy(group => group.Key.ReasonCode)
            .Select(group => $"{group.Key.ReasonCode} · {group.Key.ReasonName}").FirstOrDefault();

        // Ghép các batch trong memory theo key; không phát sinh query mới bên trong Select.
        var machines = units.Select(unit =>
        {
            runtimeByUnit.TryGetValue(unit.Id, out var runtime);
            snapshotsByUnit.TryGetValue(unit.Id, out var snapshot);
            planByUnit.TryGetValue(unit.Id, out var plan);
            var output = plan is not null && outputByPlan.TryGetValue(plan.Id, out var foundOutput) ? foundOutput : null;
            downtimeByUnit.TryGetValue(unit.Id, out var downtime);
            var total = output?.Total ?? 0;
            var reject = output?.Defect ?? 0;
            var age = runtime?.LastDataAt is { } lastData ? Math.Max(0, (long)(now - lastData).TotalSeconds) : (long?)null;
            // Stale dựa trên LastDataAt/NoData; trạng thái vận hành vẫn giữ độc lập.
            var isStale = runtime is not null && (runtime.ConnectivityState == ConnectivityStateEnum.NoData
                || !runtime.LastDataAt.HasValue || now - runtime.LastDataAt.Value > staleAfter);
            var checkpoint = runtime is not null ? checkpoints.GetValueOrDefault(runtime.DeviceId) : null;
            collectionGaps.TryGetValue(unit.Id, out var collectionGap);
            var counterIdle = checkpoint?.LastCounterChangedAt is { } changedAt
                ? Math.Max(0, (long)(now - changedAt).TotalSeconds) : (long?)null;
            var counterStalled = runtime is { ConnectivityState: ConnectivityStateEnum.Connected, OperationalState: OperationalStateEnum.Running }
                && !isStale && checkpoint?.Blocked != true && counterIdle >= staleAfter.TotalSeconds;
            var period = periodsBySite[unit.SiteId];
            var quantities = periodOutputs.GetValueOrDefault(unit.Id) ?? [];
            var production = new LiveProductionPeriods(period.Shift?.Code, period.Date, period.WeekNumber,
                period.Shift is null ? null : quantities.Sum(item => item.Shift),
                quantities.Sum(item => item.Day), quantities.Sum(item => item.Week), quantities.Sum(item => item.Month),
                plan is null || period.Shift is null ? null : quantities.Where(item => item.ProductId == plan.ProductId).Sum(item => item.Shift));
            // SQL giữ decimal không-null để tương thích schema; cờ Domain bảo toàn ý nghĩa chưa xác định.
            // Một tỷ lệ thực sự bằng 0 (ví dụ toàn sản phẩm lỗi) vẫn là 0, không đổi thành dữ liệu thiếu.
            var notApplicable = snapshot?.AnomalyFlags?.Contains("\"NotApplicable\"", StringComparison.Ordinal) == true;
            return new LiveMachineItem(unit.Id, unit.Code, unit.Name,
                (runtime?.OperationalState ?? unit.OperationalState).ToString(),
                (runtime?.ConnectivityState ?? ConnectivityStateEnum.NotConfigured).ToString(),
                notApplicable ? null : snapshot?.Oee,
                notApplicable && snapshot!.LoadingMinutes <= 0 ? null : snapshot?.Availability,
                notApplicable && snapshot!.UtilizationMinutes <= 0 ? null : snapshot?.Performance,
                notApplicable && snapshot!.Total <= 0 ? null : snapshot?.Quality,
                total, Math.Max(total - reject, 0), reject, plan?.Target ?? 0,
                checkpoint?.LastTotalCount,
                plan?.Code, plan?.ProductCode, runtime?.LastDataAt, runtime?.LastTransitionAt,
                downtime?.StartAt, downtime is null ? null : $"{downtime.ReasonCode} · {downtime.ReasonName}", isStale, age,
                production, counterIdle, counterStalled, checkpoint?.Blocked == true, checkpoint?.LastReadAt,
                collectionGap?.Quantity ?? 0, collectionGap?.At);
        }).ToArray();

        // Dùng calculator Application chung để rollup A/P/Q/OEE theo tổng số tuyệt đối.
        var rollup = DashboardRollupCalculator.Calculate(
            snapshotsByUnit.Values.Select(snapshot => new DashboardSnapshotInput(snapshot.LoadingMinutes, snapshot.UtilizationMinutes)),
            snapshotsByUnit.Values.Select(snapshot => new DashboardProductionInput(snapshot.Total, snapshot.Defect,
                snapshot.Performance * snapshot.UtilizationMinutes)));
        var summary = new LiveFleetSummary(machines.Length,
            machines.Count(machine => machine.ConnectivityState == nameof(ConnectivityStateEnum.Connected) && machine.OperationalState == nameof(OperationalStateEnum.Running)),
            machines.Count(machine => machine.ConnectivityState == nameof(ConnectivityStateEnum.Connected) && machine.OperationalState == nameof(OperationalStateEnum.Stopped)),
            machines.Count(machine => machine.ConnectivityState == nameof(ConnectivityStateEnum.Connected)),
            machines.Count(machine => machine.ConnectivityState is nameof(ConnectivityStateEnum.Disconnected) or nameof(ConnectivityStateEnum.BadTag) or nameof(ConnectivityStateEnum.NoData)),
            rollup.Oee, rollup.Availability, rollup.Performance, rollup.Quality,
            machines.Sum(machine => machine.TotalQuantity), machines.Sum(machine => machine.GoodQuantity),
            machines.Sum(machine => machine.RejectQuantity), machines.Sum(machine => machine.TargetQuantity),
            await db.OeeAlerts.AsNoTracking().CountAsync(alert => alert.State != AlertStateEnum.Resolved, ct));

        var shift = periodsBySite[units[0].SiteId].Shift;
        var sourceTimestamp = machines.Where(machine => machine.LastDataAt.HasValue).Max(machine => machine.LastDataAt);
        return new LiveOeeModel(machines, summary, shift?.Name, shift?.Start, shift?.End, shift?.Progress ?? 0,
            topReason, now, sourceTimestamp);
    }

    /// <summary>Tìm ca chứa “now” theo timezone Site, gồm cả ca bắt đầu ngày hôm trước.</summary>
    private async Task<LiveOeeCurrentShiftRow?> ResolveShiftAsync(Guid siteId, string timeZoneId, DateTimeOffset now, CancellationToken ct)
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var localDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var shifts = await db.Shifts.AsNoTracking().Where(shift => shift.SiteId == siteId && shift.Status == RecordStatusEnum.Active)
            .OrderBy(shift => shift.StartTime).Select(shift => new { shift.Code, shift.Name, shift.StartTime, shift.EndTime }).ToArrayAsync(ct);
        foreach (var date in new[] { localDate, localDate.AddDays(-1) })
            foreach (var shift in shifts)
            {
                var period = ShiftPeriodResolver.Resolve(date, shift.StartTime, shift.EndTime, zone);
                if (now < period.Start || now >= period.End) continue;
                var progress = (decimal)((now - period.Start).TotalSeconds / (period.End - period.Start).TotalSeconds);
                return new LiveOeeCurrentShiftRow(shift.Code, shift.Name, period.Start, period.End, Math.Clamp(progress, 0, 1));
            }
        return null;
    }

    private static LiveOeeModel Empty(DateTimeOffset now) => new([], new(0, 0, 0, 0, 0, null, null, null, null, 0, 0, 0, 0, 0),
        null, null, null, 0, null, now, null);

    // SQL cộng fact đã ghi nhận theo PeriodEnd; không tải từng telemetry của cả tháng về memory.
    private async Task<Dictionary<Guid, LiveOeePeriodOutputRow[]>> LoadPeriodOutputsAsync(LiveOeeUnitRow[] units,
        Dictionary<Guid, LiveOeePeriodBounds> periodsBySite, DateTimeOffset now, CancellationToken ct)
    {
        var rows = new List<LiveOeePeriodOutputRow>();
        foreach (var site in units.GroupBy(unit => unit.SiteId))
        {
            var bounds = periodsBySite[site.Key];
            var shiftStart = bounds.Shift?.Start ?? now;
            var earliest = new[] { bounds.DayStart, bounds.WeekStart, bounds.MonthStart, shiftStart }.Min();
            var ids = site.Select(unit => unit.Id).ToArray();
            rows.AddRange(await db.ProductionOutputs.AsNoTracking()
                .Where(output => ids.Contains(output.WorkUnitId) && output.PeriodEnd > earliest && output.PeriodEnd <= now)
                .GroupBy(output => new { output.WorkUnitId, output.ProductId })
                .Select(group => new LiveOeePeriodOutputRow(group.Key.WorkUnitId, group.Key.ProductId,
                    group.Sum(output => output.PeriodEnd > shiftStart ? output.TotalProducedQuantity - output.DefectQuantity : 0),
                    group.Sum(output => output.PeriodEnd > bounds.DayStart ? output.TotalProducedQuantity - output.DefectQuantity : 0),
                    group.Sum(output => output.PeriodEnd > bounds.WeekStart ? output.TotalProducedQuantity - output.DefectQuantity : 0),
                    group.Sum(output => output.PeriodEnd > bounds.MonthStart ? output.TotalProducedQuantity - output.DefectQuantity : 0)))
                .ToArrayAsync(ct));
        }
        return rows.GroupBy(row => row.WorkUnitId).ToDictionary(group => group.Key, group => group.ToArray());
    }

    private static DateTimeOffset Midnight(DateOnly date, TimeZoneInfo zone)
        => ShiftPeriodResolver.Resolve(date, TimeOnly.MinValue, TimeOnly.MinValue, zone).Start;
}
