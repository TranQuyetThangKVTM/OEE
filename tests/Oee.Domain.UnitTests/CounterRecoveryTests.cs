using Oee.Domain.Common;
using Oee.Domain.PlcIntegration;
using Oee.Tests.Fixtures;

namespace Oee.Domain.UnitTests;

public sealed class CounterRecoveryTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 3, 4, 5, 0, TimeSpan.Zero);

    [Fact]
    public void StableFreshReadsResumeFromNewBaselineWithoutCountingItsAbsoluteValue()
    {
        var checkpoint = Existing();
        for (var index = 0; index < 3; index++)
            Assert.Null(Observe(checkpoint, 173464 + index * 100, At.AddSeconds(index * 10)));
        var recovered = Observe(checkpoint, 173764, At.AddSeconds(30));

        Assert.NotNull(recovered);
        Assert.Equal("CounterRecovered", recovered.Reason);
        Assert.Equal(0, recovered.TotalDelta);
        checkpoint.Apply(recovered, "baseline", At.AddSeconds(30));
        Assert.False(checkpoint.IsDiscontinuityPending);
        Assert.Equal(1, checkpoint.CounterEpoch);
        Assert.Equal(173764, checkpoint.LastTotalCount);
        Assert.True(checkpoint.LastSampleKey!.Length <= 200);
        Assert.True(checkpoint.IsDuplicateSample("baseline"));

        var next = CounterDeltaCalculator.Calculate(checkpoint.State, new(173864, 915, At.AddSeconds(40), "next"), CounterModeEnum.Cumulative);
        Assert.False(checkpoint.RequiresRecoveryRead(next, At.AddSeconds(40)));
        checkpoint.Apply(next, "next", At.AddSeconds(40));
        Assert.Equal(100, next.TotalDelta);
        Assert.True(checkpoint.IsDuplicateSample("next"));
    }

    [Theory]
    [InlineData(0, true, false)]
    [InlineData(173464, true, false)]
    [InlineData(173464, false, true)]
    public void ZeroUnchangingOrNotificationOnlyValuesCannotConfirmRecovery(decimal total, bool refresh, bool increases)
    {
        var checkpoint = Existing();
        for (var index = 0; index < 20; index++)
        {
            var value = total + (increases ? index : 0);
            Assert.Null(Observe(checkpoint, value, At.AddSeconds(index * 10), refresh));
        }
        Assert.True(checkpoint.IsDiscontinuityPending);
        Assert.Equal(774386, checkpoint.LastTotalCount);
    }

    [Fact]
    public void IndependentTotalResetCanRecoverWhileTheAbsoluteDefectCounterRemainsHigher()
    {
        var checkpoint = Existing();
        for (var index = 0; index < 3; index++)
            Assert.Null(Observe(checkpoint, 100 + index * 10, At.AddSeconds(index * 10), defect: 4506));
        var baseline = Observe(checkpoint, 130, At.AddSeconds(30), defect: 4506)!;
        checkpoint.Apply(baseline, "baseline", At.AddSeconds(30));
        var next = CounterDeltaCalculator.Calculate(checkpoint.State,
            new(147, 4507, At.AddSeconds(40), "next"), CounterModeEnum.Cumulative);
        Assert.Equal(17, next.TotalDelta);
        Assert.Equal(1, next.DefectDelta);
        Assert.False(checkpoint.RequiresRecoveryRead(next, At.AddSeconds(40)));
    }

    [Fact]
    public void TemporaryZeroStillCountsOnlyTheDifferenceFromTheOriginalReference()
    {
        var checkpoint = Existing();
        for (var index = 0; index < 20; index++)
            Assert.Null(Observe(checkpoint, 0, At.AddSeconds(index * 10)));
        var restored = CounterDeltaCalculator.Calculate(checkpoint.State,
            new(774414, 4506, At.AddMinutes(4), "restored"), CounterModeEnum.Cumulative, isReconnect: true);
        checkpoint.Apply(restored, "restored", At.AddMinutes(4));
        Assert.Equal(28, restored.TotalDelta);
        Assert.False(checkpoint.IsDiscontinuityPending);
    }

    [Fact]
    public void OldSourceTimeOrReconnectCannotConfirmANewBaseline()
    {
        var checkpoint = Existing();
        for (var index = 0; index < 5; index++)
        {
            var received = At.AddSeconds(index * 10);
            Assert.Null(checkpoint.ObserveRecovery(new(100 + index, 0, At.AddMinutes(-5), "old"), received, true, false));
            Assert.Null(checkpoint.ObserveRecovery(new(100 + index, 0, received, "reconnect"), received, true, true));
        }
        Assert.Equal(774386, checkpoint.LastTotalCount);
    }

    [Fact]
    public void ProofAndLateOldCounterProtectionSurviveReloadingTheCheckpoint()
    {
        var checkpoint = Existing();
        Observe(checkpoint, 173464, At);
        Observe(checkpoint, 173564, At.AddSeconds(10));
        checkpoint = Reload(checkpoint);
        Assert.True(checkpoint.IsDiscontinuityPending);
        Assert.Null(Observe(checkpoint, 173664, At.AddSeconds(20)));
        var recovered = Observe(checkpoint, 173764, At.AddSeconds(30))!;
        checkpoint.Apply(recovered, "baseline", At.AddSeconds(30));
        checkpoint = Reload(checkpoint);
        Assert.True(checkpoint.IsDuplicateSample("baseline"));

        var restored = CounterDeltaCalculator.Calculate(checkpoint.State,
            new(774414, 4506, At.AddSeconds(40), "late-old-value"), CounterModeEnum.Cumulative);
        Assert.True(checkpoint.RequiresRecoveryRead(restored, At.AddSeconds(40)));
        Assert.Throws<InvalidOperationException>(() => checkpoint.Apply(restored, "late-old-value", At.AddSeconds(40)));
        Assert.Equal(173764, checkpoint.LastTotalCount);
        checkpoint.MarkDiscontinuity();
        for (var index = 0; index < 3; index++)
            Assert.Null(Observe(checkpoint, 774414 + index * 100, At.AddSeconds(40 + index * 10), defect: 4506));
        var restoredBaseline = Observe(checkpoint, 774714, At.AddSeconds(70), defect: 4506)!;
        Assert.Equal(0, restoredBaseline.TotalDelta);
        checkpoint.Apply(restoredBaseline, "restored-baseline", At.AddSeconds(70));
        var next = CounterDeltaCalculator.Calculate(checkpoint.State,
            new(774814, 4506, At.AddSeconds(80), "next"), CounterModeEnum.Cumulative);
        Assert.Equal(100, next.TotalDelta);
    }

    private static CounterCheckpointEntity Existing()
    {
        var checkpoint = CounterCheckpointEntity.Create(Guid.NewGuid());
        var old = At.AddDays(-1);
        checkpoint.Apply(CounterDeltaCalculator.Calculate(checkpoint.State, new(774386, 4506, old, "old"), CounterModeEnum.Cumulative), "old", old);
        checkpoint.MarkDiscontinuity();
        return checkpoint;
    }

    private static CounterDeltaResultValue? Observe(CounterCheckpointEntity checkpoint, decimal total, DateTimeOffset at, bool refresh = true, decimal defect = 915) =>
        checkpoint.ObserveRecovery(new(total, total == 0 ? 0 : defect, at, "probe"), at, refresh, false);

    private static CounterCheckpointEntity Reload(CounterCheckpointEntity checkpoint) => DomainTestData.Create<CounterCheckpointEntity>(
        ("PlcDeviceMappingId", checkpoint.PlcDeviceMappingId), ("LastTotalCount", checkpoint.LastTotalCount),
        ("LastDefectCount", checkpoint.LastDefectCount), ("LastReadAt", checkpoint.LastReadAt),
        ("LastCounterChangedAt", checkpoint.LastCounterChangedAt),
        ("LastSampleKey", checkpoint.LastSampleKey), ("CounterEpoch", checkpoint.CounterEpoch));
}
