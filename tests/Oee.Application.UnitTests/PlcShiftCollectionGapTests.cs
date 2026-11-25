using System.Data;
using Oee.Application.Common;
using Oee.Application.Operations;
using Oee.Application.PlcIntegration;
using Oee.Domain.Common;
using Oee.Domain.Downtime;
using Oee.Domain.Entities.Organization;
using Oee.Domain.Entities.Production;
using Oee.Domain.Platform;
using Oee.Domain.PlcIntegration;
using Oee.Domain.Production;

namespace Oee.Application.UnitTests;

public sealed class PlcShiftCollectionGapTests
{
    private static readonly DateTimeOffset Boundary = new(2026, 10, 31, 6, 0, 0, TimeSpan.FromHours(7));

    [Fact]
    public async Task ConfirmedLowerStreamResumesCountingWithoutAddingTheOldAbsoluteCounterToMorning()
    {
        var fixture = new Fixture(Boundary.AddHours(-5));
        var start = Boundary.AddMinutes(30);
        for (var index = 0; index < 3; index++)
            Assert.False((await fixture.ReceiveAsync(100 + index * 10, start.AddSeconds(index * 10), $"probe-{index}")).Accepted);
        var baseline = await fixture.ReceiveAsync(130, start.AddSeconds(30), "baseline");
        Assert.True(baseline.Accepted);
        Assert.Equal("CounterRecovered", baseline.Reason);
        Assert.Equal(0, baseline.TotalDelta);
        Assert.Empty(fixture.Outputs);
        Assert.Equal(130, fixture.Checkpoint.LastTotalCount);

        var next = await fixture.ReceiveAsync(147, start.AddSeconds(40), "next");
        Assert.True(next.Accepted);
        Assert.False(next.Quarantined);
        Assert.Equal(17, Assert.Single(fixture.Outputs).TotalProducedQuantity);
        Assert.Equal(fixture.Morning.Id, fixture.Outputs[0].ProductionPlanId);
        Assert.False((await fixture.ReceiveAsync(147, start.AddSeconds(40), "next")).Accepted);
        Assert.Single(fixture.Outputs);
    }

    [Fact]
    public async Task LateRestorationOfTheOldAbsoluteCounterCannotBypassPendingReadConfirmation()
    {
        var fixture = new Fixture(Boundary.AddHours(-5));
        var start = Boundary.AddMinutes(30);
        for (var index = 0; index < 4; index++)
            await fixture.ReceiveAsync(100 + index * 10, start.AddSeconds(index * 10), $"probe-{index}");
        await fixture.ReceiveAsync(147, start.AddSeconds(40), "counted");
        for (var index = 0; index < 4; index++)
        {
            var returned = await fixture.ReceiveAsync(1100 + index * 10, start.AddSeconds(50 + index * 10), $"old-return-{index}");
            Assert.Equal(index == 3, returned.Accepted);
            Assert.Equal(0, returned.TotalDelta);
            Assert.Single(fixture.Outputs);
        }
        await fixture.ReceiveAsync(1147, start.AddSeconds(90), "after-restored-baseline");
        Assert.Equal(34, fixture.Outputs.Sum(output => output.TotalProducedQuantity));
    }

    [Fact]
    public async Task RecoveryProofAcrossShiftBoundaryDoesNotDumpItsBaselineIntoTheNewShift()
    {
        var fixture = new Fixture(Boundary.AddHours(-5));
        var start = Boundary.AddSeconds(-20);
        for (var index = 0; index < 4; index++)
            await fixture.ReceiveAsync(100 + index * 10, start.AddSeconds(index * 10), $"probe-{index}");
        Assert.Empty(fixture.Outputs);
        Assert.False(fixture.Checkpoint.IsDiscontinuityPending);
        await fixture.ReceiveAsync(147, Boundary.AddSeconds(20), "morning");
        var output = Assert.Single(fixture.Outputs);
        Assert.Equal(fixture.Morning.Id, output.ProductionPlanId);
        Assert.Equal(17, output.TotalProducedQuantity);
    }

    [Fact]
    public async Task NightCollectionGapCannotBeDumpedIntoMorningAndNextIncrementCountsNormally()
    {
        var fixture = new Fixture(Boundary.AddHours(-5));
        var resumed = await fixture.ReceiveAsync(6000, Boundary.AddMinutes(30), "resumed");
        Assert.True(resumed.Accepted);
        Assert.True(resumed.Quarantined);
        Assert.Empty(fixture.Outputs);
        var held = Assert.Single(fixture.Quarantine);
        Assert.Equal(5000m, held.TotalDelta);
        Assert.Equal(ProductionOutputAttributionRules.CollectionGapReason, held.ReasonCode);
        Assert.Equal(6000m, fixture.Checkpoint.LastTotalCount);

        var next = await fixture.ReceiveAsync(6017, Boundary.AddMinutes(31), "morning");
        Assert.False(next.Quarantined);
        var morning = Assert.Single(fixture.Outputs);
        Assert.Equal(fixture.Morning.Id, morning.ProductionPlanId);
        Assert.Equal(17m, morning.TotalProducedQuantity);
        var replay = await fixture.ReceiveAsync(6017, Boundary.AddMinutes(31), "morning");
        Assert.False(replay.Accepted);
        Assert.Single(fixture.Quarantine);
        Assert.Single(fixture.Outputs);
        Assert.Equal(2, fixture.RawFrames.Count);
        Assert.All(fixture.RawFrames, message => Assert.NotNull(message.ProcessedAt));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(18)]
    public async Task StartupZeroCannotHideNightCollectionGapBeforeCounterRecovers(int zeroReads)
    {
        var fixture = new Fixture(Boundary.AddHours(-5));
        for (var index = 0; index < zeroReads; index++)
        {
            var startup = await fixture.ReceiveAsync(0, Boundary.AddMinutes(30).AddSeconds(index * 10), $"startup-zero-{index}");
            Assert.False(startup.Accepted);
            Assert.True(fixture.Checkpoint.IsDiscontinuityPending);
            Assert.Equal(1000m, fixture.Checkpoint.LastTotalCount);
        }

        var recoveredAt = Boundary.AddMinutes(30).AddSeconds(zeroReads * 10);
        var recovered = await fixture.ReceiveAsync(6000, recoveredAt, "restored");
        Assert.True(recovered.Accepted);
        Assert.True(recovered.Quarantined);
        Assert.Empty(fixture.Outputs);
        Assert.Equal(5000m, Assert.Single(fixture.Quarantine).TotalDelta);
        Assert.False(fixture.Checkpoint.IsDiscontinuityPending);

        await fixture.ReceiveAsync(6017, recoveredAt.AddSeconds(10), "morning");
        Assert.Equal(17m, Assert.Single(fixture.Outputs).TotalProducedQuantity);
    }

    [Fact]
    public async Task TemporaryZeroWithinOneShiftRecoversAutomaticallyWithoutDiscardingTheDelta()
    {
        var fixture = new Fixture(Boundary.AddHours(1));
        await fixture.ReceiveAsync(0, Boundary.AddHours(1).AddSeconds(10), "temporary-zero");
        var recovered = await fixture.ReceiveAsync(1028, Boundary.AddHours(1).AddSeconds(20), "restored");
        Assert.True(recovered.Accepted);
        Assert.False(recovered.Quarantined);
        Assert.False(fixture.Checkpoint.IsDiscontinuityPending);
        Assert.Equal(28m, Assert.Single(fixture.Outputs).TotalProducedQuantity);
        Assert.Empty(fixture.Quarantine);
    }

    [Fact]
    public async Task CounterConfirmedBeforeSixStillBelongsToNightEvenWhenReceivedInMorning()
    {
        var fixture = new Fixture(Boundary.AddHours(-5));
        var result = await fixture.ReceiveAsync(1010, Boundary.AddSeconds(-1), "night",
            receivedAt: Boundary.AddMinutes(30));
        Assert.False(result.Quarantined);
        Assert.Equal(fixture.Night.Id, Assert.Single(fixture.Outputs).ProductionPlanId);
        Assert.Equal(10m, fixture.Outputs.Single().TotalProducedQuantity);
        await fixture.ReceiveAsync(1027, Boundary.AddMinutes(31), "morning");
        Assert.Equal(17m, fixture.Outputs.Single(output => output.ProductionPlanId == fixture.Morning.Id).TotalProducedQuantity);
    }

    [Fact]
    public async Task NormalPollingAcrossSixUsesNewPlanWithoutQuarantiningOrExtendingIntoNight()
    {
        var fixture = new Fixture(Boundary.AddSeconds(-10));
        var result = await fixture.ReceiveAsync(1017, Boundary.AddSeconds(5), "morning");
        Assert.False(result.Quarantined);
        Assert.Empty(fixture.Quarantine);
        var output = Assert.Single(fixture.Outputs);
        Assert.Equal(17m, output.TotalProducedQuantity);
        Assert.Equal(Boundary, output.PeriodStart);
    }

    [Fact]
    public async Task LongUnchangedCounterIsNotAMissingCollectionGapWhenReadingsWereStillArriving()
    {
        var fixture = new Fixture(Boundary.AddHours(-5), observedAt: Boundary.AddSeconds(-10));
        var result = await fixture.ReceiveAsync(1017, Boundary.AddSeconds(5), "morning");
        Assert.False(result.Quarantined);
        Assert.Equal(17m, Assert.Single(fixture.Outputs).TotalProducedQuantity);
    }

    [Fact]
    public async Task GapWhollyInsideTheSamePlanDoesNotChangeShiftOwnership()
    {
        var fixture = new Fixture(Boundary.AddHours(1));
        var result = await fixture.ReceiveAsync(1017, Boundary.AddHours(3), "same-plan");
        Assert.False(result.Quarantined);
        Assert.Equal(fixture.Morning.Id, Assert.Single(fixture.Outputs).ProductionPlanId);
    }

    [Fact]
    public async Task ExactSixBoundaryClosesNightAndDoesNotStartMorningWithOldQuantity()
    {
        var fixture = new Fixture(Boundary.AddHours(-1));
        await fixture.ReceiveAsync(1010, Boundary, "night-end");
        Assert.Equal(fixture.Night.Id, Assert.Single(fixture.Outputs).ProductionPlanId);
        await fixture.ReceiveAsync(1027, Boundary.AddSeconds(10), "morning");
        Assert.Equal(17m, fixture.Outputs.Single(output => output.ProductionPlanId == fixture.Morning.Id).TotalProducedQuantity);
    }

    private sealed class Fixture : IPlcTelemetryStore, IProductionOutputStore, IDowntimeStore, IApplicationUnitOfWork, IClock
    {
        private readonly PlcTelemetryScenario scenario;
        private readonly PlcDeviceMappingEntity device;
        private PlcDeviceRuntimeStateEntity runtime;
        public CounterCheckpointEntity Checkpoint { get; private set; }
        public ProductionPlanEntity Night { get; }
        public ProductionPlanEntity Morning { get; }
        public List<ProductionOutputEntity> Outputs { get; } = [];
        public List<ProductionReconciliationItemEntity> Quarantine { get; } = [];
        public List<OutboxMessageEntity> RawFrames { get; } = [];
        public DateTimeOffset UtcNow { get; private set; }
        public bool HasChanges => true;

        internal Fixture(DateTimeOffset baselineAt, DateTimeOffset? observedAt = null)
        {
            var unitId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            device = PlcDeviceMappingEntity.Create(Guid.NewGuid(), unitId, "M11", CounterModeEnum.Cumulative, FaultCodeFormatEnum.Auto);
            Night = ProductionPlanEntity.Create(unitId, productId, "NIGHT", Boundary.AddHours(-8), Boundary, 10000);
            Morning = ProductionPlanEntity.Create(unitId, productId, "MORNING", Boundary, Boundary.AddHours(8), 10000);
            Checkpoint = CounterCheckpointEntity.Create(device.Id);
            Checkpoint.Apply(CounterDeltaCalculator.Calculate(new(null, null, null, 0),
                new(1000, 0, baselineAt, "baseline"), CounterModeEnum.Cumulative), "baseline", baselineAt);
            runtime = PlcDeviceRuntimeStateEntity.Create(device.Id, ConnectivityStateEnum.Connected,
                OperationalStateEnum.Running, observedAt ?? baselineAt);
            scenario = new(this, this, this, this, this);
        }

        internal Task<PlcProcessingResult> ReceiveAsync(decimal total, DateTimeOffset sourceAt, string key, DateTimeOffset? receivedAt = null)
        {
            UtcNow = receivedAt ?? sourceAt;
            return scenario.ProcessAsync(new(device.Id, true, total, 0, null, null, sourceAt, key,
                ReceivedAt: UtcNow, IsReadRefresh: true, TotalCounterTimestamp: sourceAt, RunningTimestamp: sourceAt), default);
        }

        public Task<PlcDeviceMappingEntity?> FindDeviceAsync(Guid id, CancellationToken ct) => Task.FromResult<PlcDeviceMappingEntity?>(device);
        public Task<CounterCheckpointEntity?> FindCheckpointAsync(Guid deviceId, CancellationToken ct) => Task.FromResult<CounterCheckpointEntity?>(Checkpoint);
        public Task<PlcDeviceRuntimeStateEntity?> FindRuntimeAsync(Guid deviceId, CancellationToken ct) => Task.FromResult<PlcDeviceRuntimeStateEntity?>(runtime);
        public Task<ProductionPlanEntity?> FindPlanAtAsync(Guid unitId, DateTimeOffset producedAt, CancellationToken ct) =>
            Task.FromResult(new[] { Night, Morning }.SingleOrDefault(plan => plan.PlannedStart < producedAt && producedAt <= plan.PlannedEnd));
        public Task<DowntimeEventEntity?> FindOpenDowntimeAsync(Guid unitId, CancellationToken ct) => Task.FromResult<DowntimeEventEntity?>(null);
        public Task<Guid?> FindFaultReasonAsync(Guid unitId, string? faultCode, CancellationToken ct) => Task.FromResult<Guid?>(null);
        public Task<Guid?> FindUnknownReasonAsync(CancellationToken ct) => Task.FromResult<Guid?>(null);
        public Task<bool> HasFaultMappingAsync(Guid unitId, string faultCode, CancellationToken ct) => Task.FromResult(false);
        public void AddCheckpoint(CounterCheckpointEntity checkpoint) => Checkpoint = checkpoint;
        public void AddRuntime(PlcDeviceRuntimeStateEntity value) => runtime = value;
        public void AddOutbox(OutboxMessageEntity message) => RawFrames.Add(message);
        public void AddUnplannedDelta(ProductionReconciliationItemEntity item) => Quarantine.Add(item);
        public void AddEvent(PlcIntegrationEventEntity item) { }
        public Task<decimal?> ReadEffectiveSpeedAsync(Guid unitId, Guid productId, DateOnly effectiveDate, CancellationToken ct) => Task.FromResult<decimal?>(100);
        public void Add(ProductionOutputEntity output) => Outputs.Add(output);
        public Task<DowntimeEventEntity?> FindAsync(Guid id, CancellationToken ct) => Task.FromResult<DowntimeEventEntity?>(null);
        public Task<DowntimeEventEntity[]> ReadAsync(Guid[] ids, CancellationToken ct) => Task.FromResult<DowntimeEventEntity[]>([]);
        public Task<DowntimeReasonEntity?> FindReasonAsync(Guid id, CancellationToken ct) => Task.FromResult<DowntimeReasonEntity?>(null);
        public Task<bool> HasOpenAsync(Guid unitId, CancellationToken ct) => Task.FromResult(false);
        public Task<bool> HasOverlapAsync(Guid unitId, DateTimeOffset start, DateTimeOffset endAt, CancellationToken ct) => Task.FromResult(false);
        public void Add(DowntimeEventEntity item) => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
        public Task<IApplicationTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken) => throw new NotSupportedException();
        public void ExpectRowVersion(SoftDeletableAuditedEntity entity, byte[] expected) { }
        public void ExpectUpdatedAt(AuditedEntity entity, DateTimeOffset? expected) { }
        public void RequireUpdatedAtWrite(AuditedEntity entity) { }
        public void AddAudit(AuditLogEntity entry) { }
        public void DiscardChanges() { }
    }
}
