using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Oee.Application.Common;
using Oee.Application.Contracts.PlcIntegration;
using Oee.Application.PlcIntegration;
using Oee.Domain.Common;
using Oee.Domain.Entities.Organization;
using Oee.Domain.Entities.Production;
using Oee.Domain.PlcIntegration;
using Oee.Infrastructure.Integrations.OpcUa;
using Oee.Infrastructure.Operations;
using Oee.Infrastructure.Persistence;
using Oee.Tests.Fixtures;

namespace Oee.Infrastructure.IntegrationTests;

public sealed class PlcCounterRecoverySqlServerTests
{
    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task BlockedCounterStillTracksRunStopAndReplayCannotChangeOperationalState()
    {
        await using var fixture = await Fixture.CreateAsync();
        if (fixture is null) return;
        var db = fixture.Db;
        var at = fixture.Clock.UtcNow;
        var telemetry = ApplicationServiceFactory.Telemetry(db, fixture.Clock);
        DeviceTelemetryFrame Frame(bool running, int received, string key) => new(fixture.Device.Id, running, 351320, 4506, null, null,
            at.AddSeconds(100), key, ReceivedAt: at.AddSeconds(received), TotalCounterTimestamp: at.AddSeconds(100),
            RunningTimestamp: running ? at.AddSeconds(2) : at);
        Assert.False((await telemetry.ProcessAsync(Frame(false, 0, "stop-1"), default)).Accepted);
        Assert.False((await telemetry.ProcessAsync(Frame(false, 1, "stop-2"), default)).Accepted);
        var downtime = await db.DowntimeEvents.SingleAsync();
        Assert.Equal(at, downtime.StartAt); // Uses Run source time, not future Total source time.
        Assert.Equal(OperationalStateEnum.Stopped, await db.PlcDeviceRuntimeStates.Select(x => x.OperationalState).SingleAsync());

        await telemetry.ProcessAsync(Frame(true, 0, "old-replay"), default);
        Assert.Null(downtime.EndAt);
        await telemetry.ProcessAsync(Frame(true, 2, "run-1"), default);
        await telemetry.ProcessAsync(Frame(true, 3, "run-2"), default);
        Assert.Equal(at.AddSeconds(2), downtime.EndAt);
        Assert.Equal(OperationalStateEnum.Running, await db.PlcDeviceRuntimeStates.Select(x => x.OperationalState).SingleAsync());
        Assert.Equal(7m, await db.ProductionOutputs.SumAsync(x => x.TotalProducedQuantity));
        Assert.Equal(838197m, await db.CounterCheckpoints.Select(x => x.LastTotalCount).SingleAsync());

        var config = await ApplicationServiceFactory.PlcConfiguration(db, fixture.Clock, new FakeOpcUaAdapter()).DeviceConfigurationAsync(fixture.Device.Id, default);
        Assert.True(config.Mapping.CounterDiscontinuityPending);
        Assert.Equal(at.AddSeconds(3), config.Mapping.LastReceivedAt);
        Assert.Equal(at.AddDays(-5), config.Mapping.LastReadAt);
        fixture.Clock.UtcNow = at.AddSeconds(4);
        var machine = Assert.Single((await new LiveOeeQueryService(db, fixture.Clock).GetAsync(new(), default)).Machines);
        Assert.True(machine.CounterDiscontinuityPending);
        Assert.False(machine.IsCounterStalled);
        Assert.Equal(at.AddSeconds(3), machine.LastDataAt);

        await telemetry.ProcessAsync(Frame(false, 4, "stop-again-1") with { RunningTimestamp = at.AddSeconds(4) }, default);
        await telemetry.ProcessAsync(Frame(false, 5, "stop-again-2") with { RunningTimestamp = at.AddSeconds(4) }, default);
        Assert.Single(await db.DowntimeEvents.Where(x => x.EndAt == null).ToArrayAsync());
        await telemetry.ProcessAsync(Frame(true, 6, "reconnect-running") with { IsReconnect = true, RunningTimestamp = at.AddSeconds(6) }, default);
        Assert.Empty(await db.DowntimeEvents.Where(x => x.EndAt == null).ToArrayAsync());
        Assert.Equal(2, await db.DowntimeEvents.CountAsync()); // No invented downtime for the connection gap.
        Assert.Equal(7m, await db.ProductionOutputs.SumAsync(x => x.TotalProducedQuantity));
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task MappingSavePreservesBlockedCheckpointAndCounterRecoversAutomatically()
    {
        await using var fixture = await Fixture.CreateAsync();
        if (fixture is null) return;
        var db = fixture.Db;
        var at = fixture.Clock.UtcNow;
        var adapter = new FakeOpcUaAdapter();
        await adapter.GetSession(fixture.Connection.Id).PublishAsync(new("Total", TagRoleEnum.TotalCounter, 351320m, at, true, null));
        await adapter.GetSession(fixture.Connection.Id).PublishAsync(new("Defect", TagRoleEnum.DefectCounter, 4506m, at, true, null));
        var trackedCheckpoint = await db.CounterCheckpoints.SingleAsync();
        DomainTestData.Set(trackedCheckpoint, "LastReadAt", at.AddMinutes(-1));
        await db.SaveChangesAsync();
        var configuration = ApplicationServiceFactory.PlcConfiguration(db, fixture.Clock, adapter);
        var before = await configuration.DeviceConfigurationAsync(fixture.Device.Id, default);
        await configuration.UpdateDeviceConfigurationAsync(fixture.Device.Id, fixture.Request(before), default);
        var checkpoint = await db.CounterCheckpoints.SingleAsync();
        Assert.Equal(838197m, checkpoint.LastTotalCount);
        Assert.Equal(1324m, checkpoint.LastDefectCount);
        Assert.True(checkpoint.IsDiscontinuityPending);
        Assert.Equal(0, checkpoint.CounterEpoch);
        Assert.Equal(7m, await db.ProductionOutputs.SumAsync(x => x.TotalProducedQuantity));
        Assert.Empty(await db.PlcIntegrationEvents.Where(x => x.Kind == "CounterRebased").ToArrayAsync());

        var telemetry = ApplicationServiceFactory.Telemetry(db, fixture.Clock);
        var next = new DeviceTelemetryFrame(fixture.Device.Id, true, 838225, 1325, null, null, at.AddSeconds(1), "next", ReceivedAt: at.AddSeconds(1));
        var recovered = await telemetry.ProcessAsync(next, default);
        Assert.Equal(28m, recovered.TotalDelta);
        Assert.False(recovered.Quarantined);
        Assert.False(checkpoint.IsDiscontinuityPending);
        Assert.Equal(35m, await db.ProductionOutputs.SumAsync(x => x.TotalProducedQuantity));
        Assert.False((await telemetry.ProcessAsync(next, default)).Accepted);
        Assert.Equal(35m, await db.ProductionOutputs.SumAsync(x => x.TotalProducedQuantity));
        await telemetry.ProcessAsync(next with { TotalCounter = 0, DefectCounter = 0, SourceTimestamp = at.AddSeconds(2), SampleKey = "startup-zero", IsReconnect = true, ReceivedAt = at.AddSeconds(2) }, default);
        Assert.Equal(838225m, checkpoint.LastTotalCount);
        var restored = await telemetry.ProcessAsync(next with { TotalCounter = 838230, DefectCounter = 1327, SourceTimestamp = at.AddSeconds(3), SampleKey = "restored", ReceivedAt = at.AddSeconds(3) }, default);
        Assert.Equal(5m, restored.TotalDelta);
        Assert.Equal(40m, await db.ProductionOutputs.SumAsync(x => x.TotalProducedQuantity));
    }

    [SqlServerTheory]
    [InlineData(0, 0)]
    [InlineData(100, 101)]
    [Trait("Category", "SqlServer")]
    public async Task MappingSaveDoesNotReadOrAdoptCurrentOpcCounterValues(decimal total, decimal defect)
    {
        await using var fixture = await Fixture.CreateAsync();
        if (fixture is null) return;
        var adapter = new FakeOpcUaAdapter();
        await adapter.GetSession(fixture.Connection.Id).PublishAsync(new("Total", TagRoleEnum.TotalCounter, total, fixture.Clock.UtcNow, true, null));
        await adapter.GetSession(fixture.Connection.Id).PublishAsync(new("Defect", TagRoleEnum.DefectCounter, defect, fixture.Clock.UtcNow, true, null));
        var service = ApplicationServiceFactory.PlcConfiguration(fixture.Db, fixture.Clock, adapter);
        var before = await service.DeviceConfigurationAsync(fixture.Device.Id, default);
        await service.UpdateDeviceConfigurationAsync(fixture.Device.Id, fixture.Request(before), default);
        Assert.Equal(838197m, await fixture.Db.CounterCheckpoints.Select(x => x.LastTotalCount).SingleAsync());
        Assert.NotEqual(before.Mapping.RowVersion, (await service.DeviceConfigurationAsync(fixture.Device.Id, default)).Mapping.RowVersion);
        Assert.True((await fixture.Db.CounterCheckpoints.SingleAsync()).IsDiscontinuityPending);
        Assert.Equal(7m, await fixture.Db.ProductionOutputs.SumAsync(x => x.TotalProducedQuantity));
        Assert.Empty(await fixture.Db.PlcIntegrationEvents.Where(x => x.Kind == "CounterRebased").ToArrayAsync());
    }

    private sealed class Fixture : IAsyncDisposable
    {
        internal OeeDbContext Db { get; }
        internal Clock Clock { get; } = new();
        internal OpcUaConnectionEntity Connection { get; private set; } = default!;
        internal PlcDeviceMappingEntity Device { get; private set; } = default!;
        private Fixture(string connection) => Db = new(new DbContextOptionsBuilder<OeeDbContext>().UseSqlServer(connection).Options);

        internal static async Task<Fixture?> CreateAsync()
        {
            var source = Environment.GetEnvironmentVariable("OEE_TEST_SQLSERVER");
            if (string.IsNullOrWhiteSpace(source)) return null;
            var connection = new SqlConnectionStringBuilder(source) { InitialCatalog = $"OeeDb_PlcRecovery_{Guid.NewGuid():N}" };
            Assert.StartsWith("OeeDb_PlcRecovery_", connection.InitialCatalog, StringComparison.Ordinal);
            var fixture = new Fixture(connection.ConnectionString);
            try { await fixture.SeedAsync(); return fixture; }
            catch { await fixture.DisposeAsync(); throw; }
        }

        private async Task SeedAsync()
        {
            await Db.Database.MigrateAsync();
            var at = Clock.UtcNow;
            var enterprise = DomainTestData.Create<EnterpriseEntity>(("Code", "E"), ("Name", "Test"));
            var site = DomainTestData.Create<SiteEntity>(("Enterprise", enterprise), ("Code", "S"), ("Name", "Test"), ("TimeZoneId", "UTC"));
            var area = DomainTestData.Create<AreaEntity>(("Site", site), ("Code", "A"), ("Name", "Test"));
            var center = DomainTestData.Create<WorkCenterEntity>(("Area", area), ("Code", "C"), ("Name", "Test"));
            var unit = DomainTestData.Create<WorkUnitEntity>(("WorkCenter", center), ("Code", "M01"), ("Name", "Test"));
            var product = DomainTestData.Create<ProductEntity>(("Code", "SKU01"), ("Name", "Test"), ("DefaultUom", "PCS"));
            var plan = DomainTestData.Create<ProductionPlanEntity>(("WorkUnit", unit), ("Product", product), ("Code", "PLAN"), ("PlannedStart", at.AddHours(-1)), ("PlannedEnd", at.AddHours(1)), ("PlannedQuantity", 100));
            DomainTestData.Set(plan, "VersionSeriesId", plan.Id);
            Connection = DomainTestData.Create<OpcUaConnectionEntity>(("Code", "OPC"), ("Name", "Test"), ("EndpointUrl", "opc.tcp://fake"), ("IsActive", true));
            Device = DomainTestData.Create<PlcDeviceMappingEntity>(("OpcUaConnection", Connection), ("WorkUnit", unit), ("DefaultProduct", product), ("ExternalDeviceId", "M01"), ("CounterMode", CounterModeEnum.Cumulative), ("DebounceSamples", 2), ("IsActive", true));
            Db.AddRange(enterprise, site, area, center, unit, product, plan, Connection, Device,
                DomainTestData.Create<CounterCheckpointEntity>(("PlcDeviceMapping", Device), ("LastTotalCount", 838197), ("LastDefectCount", 1324), ("LastReadAt", at.AddDays(-5)), ("LastSampleKey", "!counter-discontinuity"), ("LastCounterChangedAt", at.AddDays(-5))),
                PlcDeviceRuntimeStateEntity.Create(Device.Id, ConnectivityStateEnum.Connected, OperationalStateEnum.Running, at.AddSeconds(-1)),
                DomainTestData.Create<WorkUnitStandardEntity>(("WorkUnit", unit), ("Product", product), ("StandardSpeedPerMinute", 100), ("Uom", "PCS"), ("EffectiveFrom", DateOnly.FromDateTime(at.UtcDateTime).AddDays(-30))),
                DomainTestData.Create<DowntimeReasonEntity>(("Code", "DR-UNKNOWN"), ("Name", "Unknown"), ("Category", DowntimeCategoryEnum.AvailabilityLoss), ("Kind", DowntimeKindEnum.Breakdown)),
                DomainTestData.Create<ProductionOutputEntity>(("ProductionPlan", plan), ("WorkUnit", unit), ("Product", product), ("PeriodStart", at.AddMinutes(-30)), ("PeriodEnd", at.AddMinutes(-20)), ("StandardSpeedPerMinute", 100), ("TotalProducedQuantity", 7), ("Source", DataSourceEnum.Automatic)),
                OpcUaTagMappingEntity.Create(Device.Id, TagRoleEnum.Running, TagDirectionEnum.Read, "Run", OpcUaDataTypeEnum.BooleanValue, true),
                OpcUaTagMappingEntity.Create(Device.Id, TagRoleEnum.TotalCounter, TagDirectionEnum.Read, "Total", OpcUaDataTypeEnum.DoublePrecision, true),
                OpcUaTagMappingEntity.Create(Device.Id, TagRoleEnum.DefectCounter, TagDirectionEnum.Read, "Defect", OpcUaDataTypeEnum.DoublePrecision, false));
            await Db.SaveChangesAsync();
        }

        internal PlcDeviceConfigurationRequest Request(PlcDeviceConfiguration config) => new(Connection.Id, Device.WorkUnitId, Device.DefaultProductId,
            Device.ExternalDeviceId, "Cumulative", "Auto", true, 2, null, true, config.Tags, config.Mapping.RowVersion);

        public async ValueTask DisposeAsync()
        {
            await Db.Database.EnsureDeletedAsync();
            await Db.DisposeAsync();
            SqlConnection.ClearAllPools();
        }
    }

    private sealed class Clock : IClock { public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow.AddMinutes(-1); }
}
