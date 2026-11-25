using Oee.Application.Contracts.Reporting;
using Oee.Application.Reporting;

namespace Oee.Application.UnitTests;

public sealed class EquipmentTimelineBuilderTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 13, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ScheduleWithoutObservedOutputIsUnknownAndFutureIsSeparate()
    {
        var row = EquipmentTimelineBuilder.Build(Guid.NewGuid(), "M1", "Machine", Start, Start.AddHours(8), Start.AddHours(4),
            [new(Start, Start.AddHours(8), EquipmentTimelineState.Unknown)]);
        Assert.Equal(0, row.UptimeMinutes);
        Assert.Equal(EquipmentTimelineState.Unknown, row.Segments[0].State);
        Assert.Equal(Start.AddHours(4), row.Segments[0].End);
        Assert.Equal(EquipmentTimelineState.Future, row.Segments[1].State);
        Assert.Equal(Start.AddHours(8), row.Segments[1].End);
    }

    [Fact]
    public void OverlappingFailuresAreNotDoubleCountedAndStopsOverrideProduction()
    {
        var row = EquipmentTimelineBuilder.Build(Guid.NewGuid(), "M1", "Machine", Start, Start.AddHours(1), Start.AddHours(2),
            [new(Start, Start.AddHours(1), EquipmentTimelineState.Running),
                new(Start.AddMinutes(10), Start.AddMinutes(30), EquipmentTimelineState.Failure, Guid.NewGuid()),
                new(Start.AddMinutes(20), Start.AddMinutes(40), EquipmentTimelineState.Failure, Guid.NewGuid())]);
        Assert.Equal(30, row.FailureMinutes);
        Assert.Equal(30, row.UptimeMinutes);
        Assert.Equal(2, row.FailureCount);
        Assert.Equal(2, row.StopCount);
        Assert.Equal(60, row.Segments.Sum(x => (decimal)(x.End - x.Start).TotalMinutes));
    }

    [Fact]
    public void PlannedAndMinorStopsDoNotCountAsFailures()
    {
        var row = EquipmentTimelineBuilder.Build(Guid.NewGuid(), "M1", "Machine", Start, Start.AddHours(1), Start.AddHours(2),
            [new(Start, Start.AddHours(1), EquipmentTimelineState.Running),
                new(Start.AddMinutes(10), Start.AddMinutes(20), EquipmentTimelineState.PlannedStop, Guid.NewGuid()),
                new(Start.AddMinutes(30), Start.AddMinutes(40), EquipmentTimelineState.OtherStop, Guid.NewGuid())]);
        Assert.Equal(40, row.UptimeMinutes);
        Assert.Equal(0, row.FailureMinutes);
        Assert.Equal(0, row.FailureCount);
        Assert.Equal(2, row.StopCount);
    }

    [Fact]
    public void IntervalsAreClippedAndAdjacentObservedProductionIsMerged()
    {
        var row = EquipmentTimelineBuilder.Build(Guid.NewGuid(), "M1", "Machine", Start, Start.AddHours(1), Start.AddMinutes(40),
            [new(Start.AddMinutes(-20), Start.AddMinutes(20), EquipmentTimelineState.Running),
                new(Start.AddMinutes(20), Start.AddHours(2), EquipmentTimelineState.Running)]);
        Assert.Equal(40, row.UptimeMinutes);
        Assert.Equal(2, row.Segments.Count);
        Assert.Equal(Start, row.Segments[0].Start);
        Assert.Equal(Start.AddMinutes(40), row.Segments[0].End);
        Assert.Equal(EquipmentTimelineState.Future, row.Segments[1].State);
    }
}
