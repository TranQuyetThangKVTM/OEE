using Oee.Application.Common;
using Oee.Application.Oee.Snapshots.Common;
using Oee.Domain.Entities.Oee;
using Oee.Domain.Oee;

namespace Oee.Application.Oee.Snapshots.Recalculate;

/// <summary>Điều phối thao tác RecalculateOeeSnapshot trong cùng scope lưu dữ liệu.</summary>
internal sealed class RecalculateOeeSnapshotHandler(OeeSnapshotContext context)
    : IRequestHandler<RecalculateOeeSnapshotCommand, Guid>
{
    public async Task<Guid> HandleAsync(
        RecalculateOeeSnapshotCommand command,
        CancellationToken cancellationToken)
    {
        var workUnitId = command.WorkUnitId;
        var periodType = command.PeriodType;
        var periodCode = command.PeriodCode;
        var periodStart = command.PeriodStart;
        var periodEnd = command.PeriodEnd;

        if (periodEnd <= periodStart)
            throw new ArgumentException("Period end must be after period start.", nameof(command));
        var now = context.Clock.UtcNow;
        var calculationEnd = periodEnd < now ? periodEnd : now;

        if (calculationEnd <= periodStart)
            throw new ArgumentException("The OEE period has not started.", nameof(command));
        // Ca đang chạy chỉ tính thời gian đã trôi qua. Biên kỳ lưu trong SQL vẫn là biên ca đầy đủ.
        // Quantity thuộc kỳ theo thời điểm hoàn tất fact, cùng quy ước với sản lượng Live OEE.
        var outputs = await context.Store.ReadOutputsAsync(workUnitId, periodStart, calculationEnd, cancellationToken);
        var downtimes = await context.Store.ReadDowntimesAsync(workUnitId, periodStart, calculationEnd, cancellationToken);
        // Tổng quantity/duration là đầu vào tuyệt đối; tỷ lệ A/P/Q chưa được tính ở lớp EF.
        var total = outputs.Sum(x => x.TotalProducedQuantity);
        var defects = outputs.Sum(x => x.DefectQuantity);
        var unscheduled = outputs.Sum(x => x.UnscheduledMinutes);
        var idealMinutes = outputs.Sum(x => x.StandardSpeedPerMinute == 0 ? 0 : x.TotalProducedQuantity / x.StandardSpeedPerMinute);
        var idealCycle = total == 0 ? 0 : idealMinutes / total;
        // Domain engine xử lý zero denominator, clamp/anomaly và MTBF/MTTR theo Failure/Breakdown.
        var result = OeeCalculationEngine.Calculate(new OeeCalculationInputValue(
                periodStart,
                calculationEnd,
                unscheduled,
                total,
                defects,
                idealCycle,
                downtimes.Select(x => new OeeDowntimeInputValue(x.StartAt, x.EndAt ?? calculationEnd, x.Category, x.Kind))
                    .ToArray()));
        var existing = await context.Store.FindSnapshotAsync(
            workUnitId,
            periodType,
            periodCode,
            periodStart,
            OeeCalculationEngine.CalculationVersion,
            cancellationToken);

        // Idempotency: input không đổi thì không update RowVersion/audit hoặc sinh snapshot trùng.
        if (existing is not null && existing.InputHash == result.InputHash)
            return existing.Id;
        var snapshot = existing
            ?? OeeSnapshotEntity.Create(
            workUnitId,
            periodType,
            periodCode,
            periodStart,
            periodEnd,
            result,
            total,
            defects,
            now,
            OeeCalculationEngine.CalculationVersion);

        if (existing is not null)
            snapshot.ApplyCalculation(periodEnd, result, total, defects, now);

        if (existing is null)
            context.Store.Add(snapshot);
        await context.UnitOfWork.SaveChangesAsync(cancellationToken);
        await context.EvaluateAlertsAsync(snapshot, result, cancellationToken);

        return snapshot.Id;
    }
}
