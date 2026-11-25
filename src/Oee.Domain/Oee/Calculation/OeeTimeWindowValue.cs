using Oee.Domain.Common;

namespace Oee.Domain.Oee;

public sealed record OeeTimeWindowValue
{
    public DateTimeOffset Start { get; }
    public DateTimeOffset End { get; }
    public decimal Minutes => (decimal)(End - Start).TotalMinutes;

    private OeeTimeWindowValue(DateTimeOffset start, DateTimeOffset end) => (Start, End) = (start, end);

    public static OeeTimeWindowValue Create(DateTimeOffset start, DateTimeOffset end)
    {
        DomainGuard.Period(start, end);
        return new(start.ToUniversalTime(), end.ToUniversalTime());
    }

    public static OeeTimeWindowValue[] Merge(IEnumerable<OeeTimeWindowValue> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var result = new List<OeeTimeWindowValue>();
        foreach (var window in source.OrderBy(x => x.Start))
        {
            if (result.Count == 0 || window.Start > result[^1].End) result.Add(window);
            else if (window.End > result[^1].End) result[^1] = Create(result[^1].Start, window.End);
        }
        return result.ToArray();
    }

    public static OeeTimeWindowValue[] Exclude(
        IEnumerable<OeeTimeWindowValue> source,
        IEnumerable<OeeTimeWindowValue> excluded)
    {
        var result = Merge(source).ToList();
        foreach (var off in Merge(excluded))
        {
            var next = new List<OeeTimeWindowValue>();
            foreach (var window in result)
            {
                if (off.End <= window.Start || off.Start >= window.End) next.Add(window);
                else
                {
                    if (off.Start > window.Start) next.Add(Create(window.Start, off.Start));
                    if (off.End < window.End) next.Add(Create(off.End, window.End));
                }
            }
            result = next;
        }
        return result.ToArray();
    }
}
