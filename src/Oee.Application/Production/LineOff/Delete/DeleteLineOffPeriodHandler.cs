using Oee.Application.Common;

namespace Oee.Application.Production.LineOff;

internal sealed class DeleteLineOffPeriodHandler(LineOffContext context)
    : IRequestHandler<DeleteLineOffPeriodCommand, LineOffCalendar>
{
    public async Task<LineOffCalendar> HandleAsync(DeleteLineOffPeriodCommand request, CancellationToken cancellationToken)
    {
        context.EnsureManage();
        await using var session = await context.Calendar.OpenAsync(cancellationToken);
        LineOffContext.EnsureRevision(session.Calendar, request.ExpectedRevision);
        var previous = session.Calendar.Periods.SingleOrDefault(x => x.Id == request.Id && !x.Deleted)
            ?? throw new ArgumentException("Khoảng Off không còn tồn tại.");
        var entries = session.Calendar.Periods.Select(x => x.Id == previous.Id
            ? x with { Deleted = true, UpdatedAt = context.Now, UpdatedBy = context.UserName } : x).ToArray();
        var calendar = new LineOffCalendar(Guid.NewGuid(), entries);
        await session.SaveAsync(calendar, cancellationToken);
        return calendar;
    }
}
