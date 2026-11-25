using Oee.Application.Contracts.Reporting;

namespace Oee.Application.Reporting;

/// <summary>Clip, merge and prioritize intervals without counting overlap twice or inventing running history.</summary>
public static class EquipmentTimelineBuilder
{
    public static EquipmentTimelineRow Build(
        Guid unitId,
        string code,
        string name,
        DateTimeOffset start,
        DateTimeOffset end,
        DateTimeOffset now,
        IEnumerable<EquipmentTimelineSpan> source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (end <= start)
            throw new ArgumentException("Timeline end must follow start.", nameof(end));
        var observed = now < end ? now : end;
        var spans = source.Select(x => x with { Start = x.Start < start ? start : x.Start, End = x.End > observed ? observed : x.End })
            .Where(x => x.End > x.Start)
            .ToArray();
        var points = spans.SelectMany((
                x,
                index) => new[] {
                new EquipmentTimelineEdge(x.Start, index, true),
                new EquipmentTimelineEdge(x.End, index, false)
            })
            .Concat([new EquipmentTimelineEdge(start, -1, true), new EquipmentTimelineEdge(end, -1, false)])
            .Concat(now > start && now < end ? [new EquipmentTimelineEdge(now, -1, true)] : [])
            .GroupBy(x => x.At)
            .OrderBy(x => x.Key)
            .ToArray();
        var active = new SortedSet<int>(Comparer<int>.Create((left, right) =>
                {
                    var priority = (Priority(spans[right].State) + (spans[right].EventId.HasValue ? 1 : 0)).CompareTo(Priority(spans[left].State) + (spans[left].EventId.HasValue ? 1 : 0));

                    return priority != 0 ? priority : left.CompareTo(right);
                }));
        var result = new List<EquipmentTimelineSpan>();

        for (var i = 0; i < points.Length - 1; i++)
        {
            foreach (var edge in points[i])
            {
                if (edge.Index < 0)
                    continue;

                if (edge.IsStart)
                    active.Add(edge.Index);
                else
                    active.Remove(edge.Index);
            }

            var at = points[i].Key;
            var next = points[i + 1].Key;
            var segment = at >= now
                ? new EquipmentTimelineSpan(at, next, EquipmentTimelineState.Future)
                : active.Count > 0
                ? spans[active.Min] with
            {
                Start = at,
                End = next
            }

                : new EquipmentTimelineSpan(at, next, EquipmentTimelineState.NoProduction);

            if (result.Count > 0 && result[^1].End == at && result[^1].State == segment.State
                && result[^1].EventId == segment.EventId
                && result[^1].Reason == segment.Reason
                && result[^1].Note == segment.Note)
                result[^1] = result[^1] with
                {
                    End = next
                };
            else
                result.Add(segment);
        }

        var failures = spans.Where(x => x.State == EquipmentTimelineState.Failure && x.EventId.HasValue).Select(x => x.EventId).Distinct()
            .Count();
        var stops = spans.Where(x => IsStop(x.State) && x.EventId.HasValue).Select(x => x.EventId).Distinct().Count();

        return new EquipmentTimelineRow(
            unitId,
            code,
            name,
            result,
            Minutes(result.Where(x => x.State == EquipmentTimelineState.Running)),
            Minutes(result.Where(x => x.State == EquipmentTimelineState.Failure)),
            failures,
            stops);
    }

    public static bool IsStop(EquipmentTimelineState state)
        => state is EquipmentTimelineState.Failure or EquipmentTimelineState.PlannedStop or EquipmentTimelineState.OtherStop or EquipmentTimelineState.Stopped;

    private static decimal Minutes(IEnumerable<EquipmentTimelineSpan> spans)
        => spans.Sum(x => (decimal)(x.End - x.Start).TotalMinutes);

    private static int Priority(EquipmentTimelineState state)
        => state switch
        {
            EquipmentTimelineState.Failure => 60,
            EquipmentTimelineState.Stopped => 50,
            EquipmentTimelineState.OtherStop => 45,
            EquipmentTimelineState.PlannedStop => 40,
            EquipmentTimelineState.Running => 20,
            EquipmentTimelineState.Unknown => 10,
            _ => 0
        };
}
