using Oee.Application.Common;
using Oee.Domain.Common;
using Oee.Domain.Oee;

namespace Oee.Application.Production.LineOff;

internal sealed class SaveLineOffPeriodHandler(LineOffContext context)
    : IRequestHandler<SaveLineOffPeriodCommand, LineOffCalendar>
{
    public async Task<LineOffCalendar> HandleAsync(SaveLineOffPeriodCommand request, CancellationToken cancellationToken)
    {
        context.EnsureManage();
        var scope = await context.Scopes.FindAsync(request.WorkCenterId, cancellationToken)
            ?? throw new ArgumentException("Dây chuyền phải có đúng một máy Filler đang hoạt động.");
        var zone = TimeZoneInfo.FindSystemTimeZoneById(scope.TimeZoneId);
        var start = LocalInstant(request.StartLocal, zone);
        var end = LocalInstant(request.EndLocal, zone);
        var window = OeeTimeWindowValue.Create(start, end);
        if (window.End - window.Start > TimeSpan.FromDays(366))
            throw new ArgumentException("Một khoảng Off không được dài hơn 366 ngày.");
        var note = DomainGuard.Optional(request.Note, 500);

        await using var session = await context.Calendar.OpenAsync(cancellationToken);
        LineOffContext.EnsureRevision(session.Calendar, request.ExpectedRevision);
        var previous = request.Id.HasValue ? session.Calendar.Periods.SingleOrDefault(x => x.Id == request.Id && !x.Deleted) : null;
        if (request.Id.HasValue && previous is null) throw new ArgumentException("Khoảng Off không còn tồn tại.");
        if (previous is not null && previous.WorkCenterId != request.WorkCenterId)
            throw new ArgumentException("Không thể chuyển khoảng Off sang dây chuyền khác.");
        if (session.Calendar.Periods.Any(x => !x.Deleted && x.Id != request.Id && x.WorkCenterId == request.WorkCenterId
            && x.Start < end && x.End > start))
            throw new ArgumentException("Khoảng thời gian này trùng lịch Off đã lưu.");
        if (await context.Scopes.HasProductionAsync(scope.FillerId, start, end, cancellationToken))
            throw new ArgumentException("Filler đã phát sinh sản lượng trong khoảng này. Hãy chọn đúng thời gian nghỉ để không loại nhầm sản xuất.");

        var saved = new LineOffPeriod(previous?.Id ?? Guid.NewGuid(), scope.WorkCenterId, start, end,
            note, zone.Id, context.UserName, context.Now);
        var entries = session.Calendar.Periods.Where(x => x.Id != saved.Id).Append(saved).ToArray();
        var calendar = new LineOffCalendar(Guid.NewGuid(), entries);
        await session.SaveAsync(calendar, cancellationToken);
        return calendar;
    }

    private static DateTimeOffset LocalInstant(DateTime local, TimeZoneInfo zone)
    {
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(local) || zone.IsAmbiguousTime(local))
            throw new ArgumentException("Giờ nghỉ trùng thời điểm chuyển múi giờ không xác định. Hãy chọn thời điểm khác.");
        return new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
    }
}
