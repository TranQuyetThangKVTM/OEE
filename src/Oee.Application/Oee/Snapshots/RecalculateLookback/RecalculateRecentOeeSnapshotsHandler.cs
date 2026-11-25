using Oee.Application.Common;
using Oee.Application.Oee.Snapshots.Common;
using Oee.Application.Oee.Snapshots.Recalculate;
using Oee.Domain.Common;
using Oee.Domain.Organization;

namespace Oee.Application.Oee.Snapshots.RecalculateLookback;

/// <summary>Điều phối thao tác RecalculateRecentOeeSnapshots trong cùng scope lưu dữ liệu.</summary>
internal sealed class RecalculateRecentOeeSnapshotsHandler(OeeSnapshotContext context)
    : IRequestHandler<RecalculateRecentOeeSnapshotsCommand, int>
{
    public async Task<int> HandleAsync(
        RecalculateRecentOeeSnapshotsCommand command,
        CancellationToken cancellationToken)
    {
        var lookback = command.Lookback; // Chỉ chọn WorkUnit có output gần đây để tránh quét toàn bộ fleet.
        var end = context.Clock.UtcNow;
        var start = end - lookback;
        var activeIds = await context.Store.ReadActiveUnitIdsAsync(start, end, cancellationToken);
        var workUnits = await context.Store.ReadScopesAsync(activeIds, cancellationToken);
        var processed = 0;

        foreach (var workUnit in workUnits)
        {
            // DateOnly/Shift được resolve theo timezone Site, không theo local time của server.
            var zone = TimeZoneInfo.FindSystemTimeZoneById(workUnit.TimeZoneId);
            var localStart = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(start, zone).DateTime);
            var localEnd = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(end, zone).DateTime);
            var shifts = await context.Store.ReadShiftsAsync(workUnit.SiteId, cancellationToken);

            for (var date = localStart; date <= localEnd; date = date.AddDays(1))
            {
                // StartTime == EndTime được resolver hiểu là period DAY 24 giờ; ca dùng giờ cấu hình riêng.
                var day = ShiftPeriodResolver.Resolve(date, TimeOnly.MinValue, TimeOnly.MinValue, zone);

                if (day.Start < end)
                {
                    await context.Requests.ExecuteAsync(
                        new RecalculateOeeSnapshotCommand(workUnit.Id, OeePeriodTypeEnum.Day, $"DAY-{date:yyyyMMdd}", day.Start, day.End),
                        cancellationToken);
                    processed++;
                }

                foreach (var shift in shifts)
                {
                    var period = ShiftPeriodResolver.Resolve(date, shift.StartTime, shift.EndTime, zone);

                    if (period.Start >= end)
                        continue;
                    await context.Requests.ExecuteAsync(
                        new RecalculateOeeSnapshotCommand(
                            workUnit.Id,
                            OeePeriodTypeEnum.Shift,
                            $"SHIFT-{shift.Code}-{date:yyyyMMdd}",
                            period.Start,
                            period.End),
                        cancellationToken);
                    processed++;
                }
            }
        }

        return processed;
    }
}
