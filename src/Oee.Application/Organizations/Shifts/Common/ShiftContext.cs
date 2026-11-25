using Oee.Application.Common;
using Oee.Application.Operations;

namespace Oee.Application.Organizations.Shifts.Common;

/// <summary>Dependency và quy tắc dùng chung của các thao tác Shift.</summary>
internal sealed class ShiftContext(MasterDataCommandContext context) : IUseCaseContext
{
    internal MasterDataCommandContext Context => context;
    internal IMasterDataStore store => context.Store;

    internal Task EnsureUniqueShiftCodeAsync(
        Guid parent,
        string code,
        Guid? id,
        CancellationToken cancellationToken)
        => MasterDataCommandContext.UniqueAsync(
            () => store.AnyShiftsAsync(x => x.SiteId == parent && x.Code == code && x.Id != id, cancellationToken),
            "Shift code already exists in the site.");

    internal async Task EnsureShiftDoesNotOverlapAsync(
        Guid siteId,
        Guid currentId,
        TimeOnly start,
        TimeOnly end,
        CancellationToken cancellationToken)
    {
        var existing = await store.ReadShiftIntervalsAsync(siteId, currentId, cancellationToken);

        if (existing.Any(x => IntervalsOverlap(start, end, x.StartTime, x.EndTime)))
            throw new InvalidOperationException("The shift overlaps another active shift in the same site.");
    }

    internal static bool IntervalsOverlap(
        TimeOnly firstStart,
        TimeOnly firstEnd,
        TimeOnly secondStart,
        TimeOnly secondEnd)
    {
        var first = Split(firstStart, firstEnd);
        var second = Split(secondStart, secondEnd);

        return first.Any(a => second.Any(b => a.Start < b.End && b.Start < a.End));
        // Ca qua nửa đêm được tách thành [start, 1440) và [0, end) để so sánh dễ hiểu.
        static (int Start, int End)[] Split(TimeOnly start, TimeOnly end)
        {
            var from = (int)start.ToTimeSpan().TotalMinutes;
            var to = (int)end.ToTimeSpan().TotalMinutes;

            return to > from ? [(from, to)] : [(from, 1440), (0, to)];
        }
    }
}
