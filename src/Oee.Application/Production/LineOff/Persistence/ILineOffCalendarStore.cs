namespace Oee.Application.Production.LineOff;

public interface ILineOffCalendarStore
{
    Task<LineOffCalendar> ReadAsync(CancellationToken cancellationToken);
    Task<ILineOffCalendarSession> OpenAsync(CancellationToken cancellationToken);
}
