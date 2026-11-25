namespace Oee.Application.Production.LineOff;

public sealed record LineOffCalendar(Guid Revision, IReadOnlyList<LineOffPeriod> Periods)
{
    public static LineOffCalendar Empty { get; } = new(Guid.Empty, []);
}
