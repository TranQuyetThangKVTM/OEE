using Oee.Application.Common;

namespace Oee.Application.Production.Automation;

internal sealed class RunDueProductionPlanScheduleHandler(PlanAutomationContext context)
    : IRequestHandler<RunDueProductionPlanScheduleCommand, PlanAutomationResult?>
{
    public async Task<PlanAutomationResult?> HandleAsync(
        RunDueProductionPlanScheduleCommand request,
        CancellationToken cancellationToken)
    {
        if (!context.Schedules.Exists)
            return null;
        await using var session = await context.Schedules.OpenAsync(cancellationToken);
        var schedule = session.Schedule;

        if (schedule is not { Enabled: true })
            return null;
        var now = context.Now;
        var local = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZoneId));
        var today = DateOnly.FromDateTime(local.DateTime);

        if (today < schedule.FirstRunDate || TimeOnly.FromDateTime(local.DateTime) < schedule.RunAt
            || schedule.LastRunDate >= today
            || schedule.LastAttemptAt > now.AddMinutes(-5))
            return null;

        try
        {
            var (_, zone) = await context.TemplateAsync(schedule.SamplePlanId, cancellationToken);

            if (zone.Id != schedule.TimeZoneId)
                throw new ArgumentException("Múi giờ nhà máy đã đổi. Hãy mở lịch và lưu lại trước khi tiếp tục.");
            var planDate = today.AddDays(1);
            var result = await context.Plans.CreateAsync(PlanAutomationContext.Request(schedule.SamplePlanId, planDate), cancellationToken);
            var completed = new PlanAutomationResult(
                planDate,
                context.Now,
                result.CreatedCount,
                result.SkippedCount,
                result.ConflictCount,
                result.Combinations.Count(item => item.State == "Invalid"));
            await session.SaveAsync(
                schedule with
                {
                    LastRunDate = today,
                    LastAttemptAt = now,
                    LastResult = completed,
                    LastError = null
                },
                cancellationToken);

            return completed;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            context.Plans.UnitOfWork.DiscardChanges();
            var message = exception is ArgumentException or KeyNotFoundException
                ? exception.Message
                : "Không thể tạo kế hoạch. Hệ thống sẽ thử lại sau 5 phút; kiểm tra log máy chủ nếu lỗi tiếp diễn.";
            await session.SaveAsync(schedule with
                {
                    LastAttemptAt = now,
                    LastError = message
                }, cancellationToken);
            throw;
        }
    }
}
