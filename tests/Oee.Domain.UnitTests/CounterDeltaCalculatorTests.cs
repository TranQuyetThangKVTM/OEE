using Oee.Domain.Common;
using Oee.Domain.PlcIntegration;

namespace Oee.Domain.UnitTests;

/// <summary>
/// Unit test cho counter delta thuần Application.
/// Không cần PLC/SQL thật; checkpoint được truyền rõ để mô phỏng restart và idempotency.
/// </summary>
public sealed class CounterDeltaCalculatorTests
{

    [Fact]
    public void CounterCheckpointSurvivesRestartAndIsIdempotent()
    {
        // Arrange + Act: tính initial checkpoint, mẫu kế tiếp và phát lại đúng mẫu đã xử lý.
        var at = DateTimeOffset.UtcNow; var initial = CounterDeltaCalculator.Calculate(new(null, null, null, 0), new(100, 5, at, "a"), CounterModeEnum.Cumulative);
        var next = CounterDeltaCalculator.Calculate(initial.State, new(112, 7, at.AddSeconds(1), "b"), CounterModeEnum.Cumulative);
        var duplicate = CounterDeltaCalculator.Calculate(next.State, new(112, 7, at.AddSeconds(1), "b"), CounterModeEnum.Cumulative);
        // Assert: delta đúng và replay không được chấp nhận lần hai.
        Assert.Equal(12, next.TotalDelta); Assert.Equal(2, next.DefectDelta); Assert.False(duplicate.Accepted);
    }

    [Fact]
    public void CheckpointRecoversAutomaticallyFromTemporaryZeroWithoutRecountingTheAbsoluteValue()
    {
        var at = DateTimeOffset.UtcNow;
        var checkpoint = CounterCheckpointEntity.Create(Guid.NewGuid());
        checkpoint.Apply(CounterDeltaCalculator.Calculate(checkpoint.State,
            new(2833, 5, at, "old"), CounterModeEnum.Cumulative), "old", at);
        checkpoint.MarkDiscontinuity();
        var next = CounterDeltaCalculator.Calculate(checkpoint.State,
            new(2861, 7, at.AddSeconds(2), "next"), CounterModeEnum.Cumulative,
            isReconnect: checkpoint.IsDiscontinuityPending);
        checkpoint.Apply(next, "next", at.AddSeconds(2));
        Assert.False(checkpoint.IsDiscontinuityPending);
        Assert.Equal(28m, next.TotalDelta);
        Assert.Equal(2m, next.DefectDelta);
        Assert.Equal(0, checkpoint.CounterEpoch);
        Assert.False(CounterDeltaCalculator.Calculate(next.State, new(2861, 7, at.AddSeconds(2), "replay"), CounterModeEnum.Cumulative).Accepted);
    }

    [Fact]
    public void RejectedCounterSampleCannotPartiallyChangeCheckpoint()
    {
        var at = DateTimeOffset.UtcNow;
        var checkpoint = CounterCheckpointEntity.Create(Guid.NewGuid());
        checkpoint.Apply(CounterDeltaCalculator.Calculate(checkpoint.State,
            new(100, 2, at, "accepted"), CounterModeEnum.Cumulative), "accepted", at);
        var state = checkpoint.State;
        var rejected = CounterDeltaCalculator.Calculate(state,
            new(0, 0, at.AddSeconds(1), "invalid"), CounterModeEnum.Cumulative);
        Assert.Throws<InvalidOperationException>(() => checkpoint.Apply(rejected, "invalid", at.AddSeconds(1)));
        Assert.Equal(state, checkpoint.State);
        Assert.Equal("accepted", checkpoint.LastSampleKey);
        Assert.Equal(at, checkpoint.LastCounterChangedAt);
    }

    [Theory]
    [InlineData(112, 7, 12, 2)]
    [InlineData(100, 7, 0, 2)]
    public void IncreasingCountersAreNotBlockedByAnUnchangedFrameTimestamp(decimal total, decimal defect, decimal expectedTotal, decimal expectedDefect)
    {
        // Run/Defect có thể có source time mới hơn Total; timestamp lớn nhất của frame chưa đổi.
        var at = new DateTimeOffset(2026, 10, 27, 0, 0, 0, TimeSpan.Zero);
        var result = CounterDeltaCalculator.Calculate(new(100, 5, at, 0), new(total, defect, at, "changed"), CounterModeEnum.Cumulative);
        Assert.True(result.Accepted);
        Assert.Equal(expectedTotal, result.TotalDelta);
        Assert.Equal(expectedDefect, result.DefectDelta);
        Assert.False(CounterDeltaCalculator.Calculate(result.State, new(total, defect, at, "replay"), CounterModeEnum.Cumulative).Accepted);
    }

    [Theory]
    [InlineData(100, 5, 0)] // unchanged
    [InlineData(99, 5, 0)] // total decreased
    [InlineData(112, 4, 0)] // defect decreased
    [InlineData(112, 7, -1)] // older frame, even with larger counters
    public void SameOrOlderTimestampCannotConfirmResetOrRecount(decimal total, decimal defect, int seconds)
    {
        var at = new DateTimeOffset(2026, 10, 27, 0, 0, 0, TimeSpan.Zero);
        var checkpoint = new CounterStateValue(100, 5, at, 0);
        foreach (var mode in Enum.GetValues<CounterModeEnum>())
        {
            var result = CounterDeltaCalculator.Calculate(checkpoint, new(total, defect, at.AddSeconds(seconds), "old"), mode);
            Assert.False(result.Accepted);
            Assert.Equal(checkpoint, result.State);
        }
    }

    [Theory]
    [InlineData(CounterModeEnum.ResetPerShift)]
    [InlineData(CounterModeEnum.ResetDaily)]
    public void CounterResetStartsNewEpoch(CounterModeEnum mode)
    {
        // Arrange + Act: counter mới nhỏ hơn checkpoint trong mode cho phép reset.
        var at = DateTimeOffset.UtcNow; var result = CounterDeltaCalculator.Calculate(new(100, 10, at, 2), new(4, 1, at.AddSeconds(1), "x"), mode);
        // Assert: giá trị sau reset là delta mới và epoch tăng để phân biệt chu kỳ.
        Assert.Equal(4, result.TotalDelta); Assert.Equal(1, result.DefectDelta); Assert.Equal(3, result.State.Epoch);
    }

    [Fact]
    public void CounterRolloverUsesConfiguredMaximum()
    {
        // Arrange/Act/Assert: rollover 999 → 3 tạo delta 4 theo maximum đã cấu hình.
        var at = DateTimeOffset.UtcNow; var result = CounterDeltaCalculator.Calculate(new(999, 20, at, 0), new(3, 21, at.AddSeconds(1), "x"), CounterModeEnum.Cumulative, 999);
        Assert.Equal(4, result.TotalDelta);
    }

    [Fact]
    public void TemporaryServerZeroDoesNotRecountTheRestoredCumulativeCounter()
    {
        var at = DateTimeOffset.UtcNow;
        var checkpoint = new CounterStateValue(2833, 0, at, 0);
        var zero = CounterDeltaCalculator.Calculate(checkpoint, new(0, 0, at.AddSeconds(1), "zero"), CounterModeEnum.Cumulative);
        var restored = CounterDeltaCalculator.Calculate(zero.State, new(2861, 0, at.AddSeconds(2), "restored"), CounterModeEnum.Cumulative);

        Assert.False(zero.Accepted);
        Assert.Equal("UnexpectedCounterDecrease", zero.Reason);
        Assert.Equal(checkpoint, zero.State);
        Assert.Equal(28m, restored.TotalDelta);
        Assert.Equal(0, restored.State.Epoch);
    }

    [Theory]
    [InlineData(2800, 0)]
    [InlineData(2834, 0)]
    public void UnconfirmedDecreaseOfEitherCumulativeCounterPreservesBothCheckpoints(decimal total, decimal defect)
    {
        var at = DateTimeOffset.UtcNow;
        var checkpoint = new CounterStateValue(2833, 2, at, 4);
        var result = CounterDeltaCalculator.Calculate(checkpoint, new(total, defect, at.AddSeconds(1), "lower"), CounterModeEnum.Cumulative);
        Assert.False(result.Accepted);
        Assert.Equal(checkpoint, result.State);
        Assert.Equal(0, result.TotalDelta);
        Assert.Equal(0, result.DefectDelta);
    }

    [Theory]
    [InlineData(CounterModeEnum.Cumulative)]
    [InlineData(CounterModeEnum.ResetPerShift)]
    [InlineData(CounterModeEnum.ResetDaily)]
    public void ReconnectCannotConfirmCounterReset(CounterModeEnum mode)
    {
        var at = DateTimeOffset.UtcNow;
        var checkpoint = new CounterStateValue(100, 2, at, 0);
        var result = CounterDeltaCalculator.Calculate(checkpoint, new(4, 0, at.AddSeconds(1), "reconnect"), mode, isReconnect: true);
        Assert.False(result.Accepted);
        Assert.Equal("ReconnectCounterDecrease", result.Reason);
        Assert.Equal(checkpoint, result.State);
    }

    [Fact]
    public void ReconnectWithMonotonicCounterPreservesProductionDuringDisconnect()
    {
        var at = DateTimeOffset.UtcNow;
        var result = CounterDeltaCalculator.Calculate(new(2833, 0, at, 1), new(2861, 0, at.AddMinutes(2), "reconnect"), CounterModeEnum.Cumulative, isReconnect: true);
        Assert.True(result.Accepted);
        Assert.Equal(28m, result.TotalDelta);
    }

    [Theory]
    [InlineData(CounterModeEnum.Cumulative)]
    [InlineData(CounterModeEnum.ResetPerShift)]
    [InlineData(CounterModeEnum.ResetDaily)]
    public void ReconnectStartupZeroWithOldSourceTimeStillSignalsDiscontinuity(CounterModeEnum mode)
    {
        var at = DateTimeOffset.UtcNow;
        var checkpoint = new CounterStateValue(2833, 2, at, 0);
        var result = CounterDeltaCalculator.Calculate(checkpoint, new(0, 0, at.AddMinutes(-1), "startup"), mode, isReconnect: true);
        Assert.False(result.Accepted);
        Assert.Equal("ReconnectCounterDecrease", result.Reason);
        Assert.Equal(checkpoint, result.State);
    }
}
