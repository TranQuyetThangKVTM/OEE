using Oee.Application.Modules;
using Oee.Domain.Common;
using Oee.Domain.Oee;

namespace Oee.Application.UnitTests;

public sealed class LineOeeCalculationTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 17, 6, 0, 0, TimeSpan.FromHours(7));

    [Fact]
    public void HalfShiftOffRemovesOnlyTheOffHoursAndItsDowntime()
    {
        var plan = OeeTimeWindowValue.Create(Start, Start.AddHours(8));
        var off = OeeTimeWindowValue.Create(Start.AddHours(4), Start.AddHours(8));
        var stop = new OeeDowntimeInputValue(Start.AddHours(3), Start.AddHours(8),
            DowntimeCategoryEnum.AvailabilityLoss, DowntimeKindEnum.Breakdown);
        var remaining = OeeTimeWindowValue.Exclude([plan], [off]);
        var result = LineOeeCalculator.Calculate(Start, Start.AddHours(8), remaining, new(180, 0, 180), [stop]);

        Assert.Equal(240m, result.LoadingMinutes);
        Assert.Equal(180m, result.UtilizationMinutes);
        Assert.Equal(.75m, result.Availability);
        Assert.Equal(1m, result.Performance);
        Assert.Equal(.75m, result.Oee);
    }

    [Fact]
    public void StoppedWholeDayCountsButConfirmedOffDoesNotCountInMonth()
    {
        var first = OeeTimeWindowValue.Create(Start, Start.AddHours(8));
        var second = OeeTimeWindowValue.Create(Start.AddDays(1), Start.AddDays(1).AddHours(8));
        var stop = new OeeDowntimeInputValue(second.Start, second.End,
            DowntimeCategoryEnum.AvailabilityLoss, DowntimeKindEnum.Breakdown);
        var production = new DashboardProductionInput(432, 0, 432);
        var withStop = LineOeeCalculator.Calculate(first.Start, second.End, [first, second], production, [stop]);
        var withOff = LineOeeCalculator.Calculate(first.Start, second.End,
            OeeTimeWindowValue.Exclude([first, second], [second]), production, [stop]);
        var stoppedDay = LineOeeCalculator.Calculate(second.Start, second.End, [second], new(0, 0, 0), [stop]);
        var offDay = LineOeeCalculator.Calculate(second.Start, second.End, [], new(0, 0, 0), [stop]);

        Assert.Equal(.45m, withStop.Oee);
        Assert.Equal(.9m, withOff.Oee);
        Assert.Equal(0m, stoppedDay.Oee);
        Assert.Equal(480m, stoppedDay.LoadingMinutes);
        Assert.Null(offDay.Oee);
        Assert.Equal(0m, offDay.LoadingMinutes);
    }

    [Fact]
    public void OverlappingPlansAndOffPeriodsAreMergedAndNightOffSplitsExactly()
    {
        var night = OeeTimeWindowValue.Create(Start.AddHours(16), Start.AddHours(24)); // 22:00–06:00.
        var windows = OeeTimeWindowValue.Exclude([night, night],
            [OeeTimeWindowValue.Create(night.Start.AddHours(2), night.Start.AddHours(4)),
             OeeTimeWindowValue.Create(night.Start.AddHours(3), night.Start.AddHours(5))]);
        Assert.Equal(2, windows.Length);
        Assert.Equal(300m, windows.Sum(x => x.Minutes));
        Assert.Equal(night.Start.AddHours(2), windows[0].End);
        Assert.Equal(night.Start.AddHours(5), windows[1].Start);
        Assert.Throws<ArgumentException>(() => OeeTimeWindowValue.Create(Start, Start));
    }
}
