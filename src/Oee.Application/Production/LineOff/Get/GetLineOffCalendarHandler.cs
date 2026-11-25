using Oee.Application.Common;

namespace Oee.Application.Production.LineOff;

internal sealed class GetLineOffCalendarHandler(ILineOffCalendarStore store)
    : IRequestHandler<GetLineOffCalendarQuery, LineOffCalendar>
{
    public Task<LineOffCalendar> HandleAsync(GetLineOffCalendarQuery request, CancellationToken cancellationToken)
        => store.ReadAsync(cancellationToken);
}
