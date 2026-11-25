using System.Globalization;
using Oee.Application.Common;
using Oee.Application.Contracts.Reporting;
using Oee.Application.Reporting;
using Oee.Domain.Organization;

namespace Oee.Application.Production.Automation;

internal sealed class RunDueShiftReportsHandler(
    PlanAutomationContext context,
    IReportAnalysisQueryService reports,
    IReportExportService exports,
    IAutomaticShiftReportFiles files)
    : IRequestHandler<RunDueShiftReportsCommand, int>
{
    public async Task<int> HandleAsync(
        RunDueShiftReportsCommand request,
        CancellationToken cancellationToken)
    {
        if (!context.Schedules.Exists)
            return 0;
        await using var session = await context.Schedules.OpenAsync(cancellationToken);
        var schedule = session.Schedule;

        if (schedule is not { PdfEnabled: true, PdfEnabledAt: not null })
            return 0;
        var now = context.Now;

        if (schedule.PdfLastError is not null && schedule.PdfLastAttemptAt > now.AddMinutes(-5))
            return 0;

        try
        {
            var (_, zone) = await context.TemplateAsync(schedule.SamplePlanId, cancellationToken);

            if (zone.Id != schedule.TimeZoneId)
                throw new ArgumentException("Múi giờ nhà máy đã đổi. Hãy lưu lại lịch tự động.");
            var items = await context.Plans.Store.ReadSampleItemsAsync(schedule.SamplePlanId, cancellationToken);
            var shifts = items.Select(item => item.Shift).DistinctBy(shift => shift.Id).ToArray();
            var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
            var enabledDate = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(schedule.PdfEnabledAt.Value, zone).DateTime).AddDays(-1);
            // Chạy bù tối đa 7 ngày; không xuất lại lịch sử trước khi người quản lý bật chức năng.
            var from = enabledDate > today.AddDays(-7) ? enabledDate : today.AddDays(-7);
            var count = 0;

            for (var date = from; date <= today; date = date.AddDays(1))
            {
                foreach (var shift in shifts)
                {
                    var period = ShiftPeriodResolver.Resolve(date, shift.StartTime, shift.EndTime, zone);
                    var cursor = schedule.PdfCursors?.FirstOrDefault(item => item.ShiftId == shift.Id);

                    if (period.End < schedule.PdfEnabledAt || period.End.AddMinutes(schedule.PdfDelayMinutes) > now
                        || cursor?.LastCompletedEnd >= period.End)
                        continue;
                    var query = new ReportQuery(
                        ReportKind.ShiftKpi,
                        date,
                        date,
                        SiteId: shift.SiteId,
                        ShiftId: shift.Id,
                        PageSize: 5000);
                    var data = await reports.QueryAsync(query, cancellationToken);

                    // Không âm thầm xuất trang đầu khi nhà máy có nhiều hơn giới hạn mỗi trang.
                    if (data.TotalCount > data.Rows.Count)
                        throw new InvalidOperationException("Báo cáo vượt giới hạn 5000 dòng; cần thu hẹp phạm vi xuất.");
                    var id = Guid.NewGuid();
                    var safeCode = string.Concat(shift.Code.Where(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_'));
                    var fileName = FormattableString.Invariant($"OEE-Ca-{safeCode}-{date:yyyyMMdd}.pdf");
                    var document = new GeneratedShiftReport(
                        id,
                        shift.Id,
                        shift.Code,
                        date,
                        period.End,
                        context.Now,
                        fileName,
                        data.HasData);
                    var content = exports.RenderPdf(data with
                        {
                            Title = $"OEE theo ca · {shift.Code}",
                            FilterSummary = data.FilterSummary + string.Create(
                                CultureInfo.InvariantCulture,
                                $" · Ca {period.Start:dd/MM/yyyy HH:mm}–{period.End:dd/MM/yyyy HH:mm}")
                        });
                    await files.SaveAsync(id, content, cancellationToken);
                    var history = (schedule.Reports ?? []).Append(document).OrderByDescending(item => item.GeneratedAt).ToArray();
                    var cursors = (schedule.PdfCursors ?? []).Where(item => item.ShiftId != shift.Id)
                        .Append(new ShiftReportCursor(shift.Id, period.End))
                        .ToArray();
                    var updated = schedule with
                    {
                        Reports = history.Take(90).ToArray(),
                        PdfCursors = cursors,
                        PdfLastAttemptAt = now,
                        PdfLastError = null
                    };

                    try
                    {
                        await session.SaveAsync(updated, cancellationToken);
                    }
                    catch
                    {
                        // Ghi index chưa thành công: không để lại một PDF không có liên kết rồi sinh thêm ở lần retry.
                        await files.DeleteAsync(id, CancellationToken.None);
                        throw;
                    }
                    schedule = updated;

                    // Xóa file cũ chỉ sau khi index mới đã lưu; không mất file còn được liên kết.
                    foreach (var old in history.Skip(90))
                        await files.DeleteAsync(old.Id, cancellationToken);
                    count++;
                }
            }

            if (schedule.PdfLastError is not null)
                await session.SaveAsync(schedule with
                    {
                        PdfLastError = null,
                        PdfLastAttemptAt = now
                    }, cancellationToken);

            return count;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var message = exception is ArgumentException
                ? exception.Message
                : "Không thể xuất PDF ca. Hệ thống sẽ thử lại sau 5 phút; kiểm tra log máy chủ nếu lỗi tiếp diễn.";
            await session.SaveAsync(schedule with
                {
                    PdfLastAttemptAt = now,
                    PdfLastError = message
                }, cancellationToken);
            throw;
        }
    }
}
