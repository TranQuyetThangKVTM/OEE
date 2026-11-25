namespace Oee.Application.Production.LineOff;

public interface ILineOffCalendarSession : IAsyncDisposable
{
    LineOffCalendar Calendar { get; }
    Task SaveAsync(LineOffCalendar calendar, CancellationToken cancellationToken);
}
