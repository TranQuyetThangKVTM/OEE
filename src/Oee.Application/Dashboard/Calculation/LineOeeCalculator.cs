using Oee.Domain.Oee;

namespace Oee.Application.Modules;

public static class LineOeeCalculator
{
    public static DashboardRollupResult Calculate(
        DateTimeOffset start,
        DateTimeOffset end,
        IReadOnlyCollection<OeeTimeWindowValue> operatingWindows,
        DashboardProductionInput production,
        IReadOnlyCollection<OeeDowntimeInputValue> downtimes,
        decimal unscheduledMinutes = 0)
    {
        var windows = OeeTimeWindowValue.Merge(operatingWindows)
            .Where(x => x.Start < end && x.End > start)
            .Select(x => OeeTimeWindowValue.Create(x.Start < start ? start : x.Start, x.End > end ? end : x.End)).ToArray();
        var loading = windows.Sum(x => x.Minutes);
        var clippedStops = downtimes.SelectMany(stop => windows
            .Where(window => stop.StartAt < window.End && stop.EndAt > window.Start)
            .Select(window => new OeeDowntimeInputValue(stop.StartAt > window.Start ? stop.StartAt : window.Start,
                stop.EndAt < window.End ? stop.EndAt : window.End, stop.Category, stop.Kind))).ToArray();
        var result = OeeCalculationEngine.Calculate(new(start, end,
            (decimal)(end - start).TotalMinutes - loading + unscheduledMinutes,
            production.TotalQuantity, production.DefectQuantity,
            production.TotalQuantity == 0 ? 0 : production.IdealRunMinutes / production.TotalQuantity, clippedStops));
        return new(result.LoadingMinutes, result.UtilizationMinutes, production.IdealRunMinutes,
            production.TotalQuantity, result.GoodQuantity, production.DefectQuantity,
            result.Availability, result.Performance, result.Quality,
            result.LoadingMinutes > 0 && production.TotalQuantity == 0 ? 0 : result.Oee);
    }
}
