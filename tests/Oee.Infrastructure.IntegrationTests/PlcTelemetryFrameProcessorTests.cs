using Oee.Application.Common;
using Oee.Application.PlcIntegration;
using Oee.Domain.Common;
using Oee.Domain.PlcIntegration;
using Oee.Infrastructure.BackgroundServices;

namespace Oee.Infrastructure.IntegrationTests;

public sealed class PlcTelemetryFrameProcessorTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 31, 6, 0, 0, TimeSpan.FromHours(7));

    [Fact]
    public async Task ConcurrencyConflictRetriesTheSameFrameWithoutDroppingItsIncrement()
    {
        var calls = new List<DeviceTelemetryFrame>();
        var processor = new PlcTelemetryFrameProcessor((frame, _) =>
        {
            calls.Add(frame);
            if (calls.Count < 3)
                throw new ConcurrencyConflictException("Runtime updated concurrently.");
            return Task.FromResult(Calculate(frame, CounterModeEnum.Cumulative));
        });

        var sample = Frame(Guid.NewGuid(), 2861, reconnect: true);
        var result = await processor.ProcessAsync(sample, default);
        Assert.True(result.Accepted);
        Assert.Equal(28m, result.TotalDelta);
        Assert.Equal(3, calls.Count);
        Assert.All(calls, frame => Assert.Equal(sample, frame));
    }

    [Theory]
    [InlineData(CounterModeEnum.ResetPerShift)]
    [InlineData(CounterModeEnum.ResetDaily)]
    public async Task FailedInitialReadCannotLetNextStartupZeroResetTheCheckpoint(CounterModeEnum mode)
    {
        var calls = 0;
        var processor = new PlcTelemetryFrameProcessor((frame, _) =>
        {
            if (++calls == 1)
                throw new PersistenceWriteException("SQL temporarily unavailable.", new InvalidOperationException("Test failure."));
            return Task.FromResult(Calculate(frame, mode));
        });
        var id = Guid.NewGuid();
        await Assert.ThrowsAsync<PersistenceWriteException>(() =>
            processor.ProcessAsync(Frame(id, 0, reconnect: true), default));

        var next = await processor.ProcessAsync(Frame(id, 0), default);
        Assert.False(next.Accepted);
        Assert.Equal("ReconnectCounterDecrease", next.Reason);
        Assert.Equal(0m, next.TotalDelta);
    }

    [Fact]
    public async Task ExhaustedConcurrencyRetriesKeepReconnectProtectionForTheFollowingFrame()
    {
        var calls = 0;
        var processor = new PlcTelemetryFrameProcessor((frame, _) =>
        {
            if (++calls <= 3)
                throw new ConcurrencyConflictException("Conflicting RowVersion.");
            return Task.FromResult(Calculate(frame, CounterModeEnum.ResetPerShift));
        });
        var id = Guid.NewGuid();
        await Assert.ThrowsAsync<ConcurrencyConflictException>(() =>
            processor.ProcessAsync(Frame(id, 0, reconnect: true), default));
        Assert.Equal(3, calls);
        Assert.False((await processor.ProcessAsync(Frame(id, 0), default)).Accepted);
    }

    [Theory]
    [InlineData("DuplicateSampleKey")]
    [InlineData("OutOfOrderOrDuplicate")]
    public async Task DuplicateReconnectSampleDoesNotRemoveProtection(string reason)
    {
        var calls = 0;
        var processor = new PlcTelemetryFrameProcessor((frame, _) => Task.FromResult(++calls == 1
            ? new PlcProcessingResult(false, 0, 0, false, reason)
            : Calculate(frame, CounterModeEnum.ResetPerShift)));
        var id = Guid.NewGuid();
        await processor.ProcessAsync(Frame(id, 2833, reconnect: true), default);
        Assert.False((await processor.ProcessAsync(Frame(id, 0), default)).Accepted);
    }

    [Fact]
    public async Task PendingReconnectDoesNotAffectAnotherDevice()
    {
        var id = Guid.NewGuid();
        var processor = new PlcTelemetryFrameProcessor((frame, _) =>
        {
            if (frame.DeviceMappingId == id)
                throw new PersistenceWriteException("Temporary failure.", new InvalidOperationException("Test failure."));
            Assert.False(frame.IsReconnect);
            return Task.FromResult(Calculate(frame, CounterModeEnum.ResetPerShift));
        });
        await Assert.ThrowsAsync<PersistenceWriteException>(() =>
            processor.ProcessAsync(Frame(id, 0, reconnect: true), default));
        Assert.True((await processor.ProcessAsync(Frame(Guid.NewGuid(), 0), default)).Accepted);
    }

    private static DeviceTelemetryFrame Frame(Guid id, decimal total, bool reconnect = false) =>
        new(id, true, total, 0, null, null, At.AddSeconds(1), "sample", IsReconnect: reconnect);

    private static PlcProcessingResult Calculate(DeviceTelemetryFrame frame, CounterModeEnum mode)
    {
        var delta = CounterDeltaCalculator.Calculate(new(2833, 0, At, 0),
            new(frame.TotalCounter, 0, frame.SourceTimestamp, frame.SampleKey), mode,
            isReconnect: frame.IsReconnect);
        return new(delta.Accepted, delta.TotalDelta, delta.DefectDelta, false, delta.Reason);
    }
}
