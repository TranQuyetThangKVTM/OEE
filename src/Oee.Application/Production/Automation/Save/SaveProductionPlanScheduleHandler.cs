using Oee.Application.Common;

namespace Oee.Application.Production.Automation;

internal sealed class SaveProductionPlanScheduleHandler(PlanAutomationContext context)
    : IRequestHandler<SaveProductionPlanScheduleCommand, ProductionPlanSchedule>
{
    public async Task<ProductionPlanSchedule> HandleAsync(
        SaveProductionPlanScheduleCommand request,
        CancellationToken cancellationToken)
    {
        context.EnsureManage();
        await using var session = await context.Schedules.OpenAsync(cancellationToken);
        var previous = session.Schedule;

        if (previous?.Revision != request.ExpectedRevision)
            throw new ArgumentException("Lịch đã được người khác thay đổi. Đóng và mở lại popup để tải lịch mới nhất.");

        // Cho phép tắt lịch ngay cả khi mẫu cũ đã bị xóa hoặc các dòng mẫu không còn hợp lệ.
        if (!request.Enabled && !request.ExportShiftPdf && previous is not null)
        {
            var disabled = previous with
            {
                Enabled = false,
                PdfEnabled = false,
                Revision = Guid.NewGuid(),
                UpdatedAt = context.Now,
                UpdatedBy = context.Actor.UserName,
                UpdatedById = context.Actor.UserId
            };
            await session.SaveAsync(disabled, cancellationToken);

            return disabled;
        }

        if (request.SamplePlanId == Guid.Empty)
            throw new ArgumentException("Hãy chọn kế hoạch mẫu.");

        if (request.PdfDelayMinutes is < 1 or > 120)
            throw new ArgumentException("Thời gian chờ xuất PDF phải từ 1 đến 120 phút sau khi hết ca.");
        var (code, zone) = await context.TemplateAsync(request.SamplePlanId, cancellationToken);
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(context.Now, zone).DateTime);
        var candidates = await context.Plans.BuildCandidatesAsync(
            PlanAutomationContext.Request(request.SamplePlanId, today.AddDays(1)),
            cancellationToken);

        if (candidates.Any(candidate => candidate.State != "Valid"))
            throw new ArgumentException("Kế hoạch mẫu có dòng không hợp lệ. Kiểm tra thiết bị, ca, SKU và sản lượng trước khi bật lịch.");
        var sameTemplate = previous?.SamplePlanId == request.SamplePlanId;
        var saved = new ProductionPlanSchedule(
            Guid.NewGuid(),
            request.Enabled,
            request.SamplePlanId,
            code,
            new TimeOnly(request.RunAt.Hour, request.RunAt.Minute),
            zone.Id,
            today,
            context.Actor.UserId,
            context.Actor.UserName,
            context.Now,
            sameTemplate ? previous?.LastRunDate : null,
            sameTemplate ? previous?.LastAttemptAt : null,
            sameTemplate ? previous?.LastResult : null,
            sameTemplate ? previous?.LastError : null,
            request.ExportShiftPdf,
            request.PdfDelayMinutes,
            request.ExportShiftPdf
                ? sameTemplate && previous?.PdfEnabled == true ? previous.PdfEnabledAt : context.Now
                : previous?.PdfEnabledAt,
            previous?.Reports,
            sameTemplate ? previous?.PdfCursors : null);
        await session.SaveAsync(saved, cancellationToken);

        return saved;
    }
}
