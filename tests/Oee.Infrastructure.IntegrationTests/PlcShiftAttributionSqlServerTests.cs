using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Oee.Application.Common;
using Oee.Application.PlcIntegration;
using Oee.Domain.Common;
using Oee.Domain.Entities.Organization;
using Oee.Domain.Entities.Production;
using Oee.Domain.PlcIntegration;
using Oee.Infrastructure.BackgroundServices;
using Oee.Infrastructure.BackgroundServices.Configuration;
using Oee.Infrastructure.Integrations.OpcUa;
using Oee.Infrastructure.Operations;
using Oee.Infrastructure.Persistence;
using Oee.Tests.Fixtures;

namespace Oee.Infrastructure.IntegrationTests;

public sealed class PlcShiftAttributionSqlServerTests
{
    [SqlServerTheory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [Trait("Category", "SqlServer")]
    public async Task NewRunTimestampCannotMovePreviousShiftCounterIntoTheNightShift(bool exactlyAtBoundary, bool runTimestampAhead)
    {
        var source = Environment.GetEnvironmentVariable("OEE_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(source)) return;
        var connection = new SqlConnectionStringBuilder(source) { InitialCatalog = $"OeeDb_ShiftCounter_{Guid.NewGuid():N}" };
        Assert.StartsWith("OeeDb_ShiftCounter_", connection.InitialCatalog, StringComparison.Ordinal);
        await using var db = new OeeDbContext(new DbContextOptionsBuilder<OeeDbContext>().UseSqlServer(connection.ConnectionString).Options);
        try
        {
            await db.Database.MigrateAsync();
            var boundary = new DateTimeOffset(2026, 10, 15, 22, 0, 0, TimeSpan.FromHours(7)).ToUniversalTime();
            var oldCounterAt = exactlyAtBoundary ? boundary : boundary.AddSeconds(-1);
            var now = boundary.AddHours(1).AddMinutes(7);
            var clock = new TestClock(now);
            var enterprise = DomainTestData.Create<EnterpriseEntity>(("Code", "E"), ("Name", "Test"));
            var site = DomainTestData.Create<SiteEntity>(("Enterprise", enterprise), ("Code", "VN"), ("Name", "Test"), ("TimeZoneId", "Asia/Ho_Chi_Minh"));
            var area = DomainTestData.Create<AreaEntity>(("Site", site), ("Code", "A"), ("Name", "Test"));
            var center = DomainTestData.Create<WorkCenterEntity>(("Area", area), ("Code", "C"), ("Name", "Test"));
            var unit = DomainTestData.Create<WorkUnitEntity>(("WorkCenter", center), ("Code", "M11"), ("Name", "Pallet robot"));
            var product = DomainTestData.Create<ProductEntity>(("Code", "SKU01"), ("Name", "Test"), ("DefaultUom", "PCS"));
            var afternoon = DomainTestData.Create<ShiftEntity>(("Site", site), ("Code", "S02"), ("Name", "Afternoon"), ("StartTime", (global::System.TimeOnly)(new(14, 0))), ("EndTime", (global::System.TimeOnly)(new(22, 0))));
            var night = DomainTestData.Create<ShiftEntity>(("Site", site), ("Code", "S03"), ("Name", "Night"), ("StartTime", (global::System.TimeOnly)(new(22, 0))), ("EndTime", (global::System.TimeOnly)(new(6, 0))));
            var oldPlan = DomainTestData.Create<ProductionPlanEntity>(("WorkUnit", unit), ("Product", product), ("Shift", afternoon), ("Code", "AFTERNOON"), ("PlannedStart", boundary.AddHours(-8)), ("PlannedEnd", boundary), ("PlannedQuantity", 100));
            var newPlan = DomainTestData.Create<ProductionPlanEntity>(("WorkUnit", unit), ("Product", product), ("Shift", night), ("Code", "NIGHT"), ("PlannedStart", boundary), ("PlannedEnd", boundary.AddHours(8)), ("PlannedQuantity", 100));
            DomainTestData.Set(oldPlan, "VersionSeriesId", oldPlan.Id);
            DomainTestData.Set(newPlan, "VersionSeriesId", newPlan.Id);
            var opc = DomainTestData.Create<OpcUaConnectionEntity>(("Code", "OPC"), ("Name", "Test"), ("EndpointUrl", "opc.tcp://fake"), ("IsActive", true));
            var device = DomainTestData.Create<PlcDeviceMappingEntity>(("OpcUaConnection", opc), ("WorkUnit", unit), ("ExternalDeviceId", "M11"), ("IsActive", true), ("CounterMode", CounterModeEnum.Cumulative));
            db.AddRange(enterprise, site, area, center, unit, product, afternoon, night, oldPlan, newPlan, opc, device,
                DomainTestData.Create<WorkUnitStandardEntity>(("WorkUnit", unit), ("Product", product), ("Uom", "PCS"), ("StandardSpeedPerMinute", .2m), ("EffectiveFrom", (global::System.DateOnly)(new(2026, 1, 1)))),
                DomainTestData.Create<CounterCheckpointEntity>(("PlcDeviceMapping", device), ("LastTotalCount", 3373), ("LastDefectCount", 0), ("LastReadAt", boundary.AddMinutes(-2)), ("LastSampleKey", "previous")),
                DomainTestData.Create<OpcUaTagMappingEntity>(("PlcDeviceMapping", device), ("NodeId", "Run"), ("TagRole", TagRoleEnum.Running), ("Direction", TagDirectionEnum.Read), ("DataType", OpcUaDataTypeEnum.BooleanValue), ("IsRequired", true)),
                DomainTestData.Create<OpcUaTagMappingEntity>(("PlcDeviceMapping", device), ("NodeId", "Total"), ("TagRole", TagRoleEnum.TotalCounter), ("Direction", TagDirectionEnum.Read), ("DataType", OpcUaDataTypeEnum.DoublePrecision), ("IsRequired", true)));
            await db.SaveChangesAsync();
            var fake = new FakeOpcUaAdapter();
            var session = fake.GetSession(opc.Id);
            await session.PublishAsync(new("Run", TagRoleEnum.Running, true, runTimestampAhead ? now.AddMinutes(5) : boundary.AddMinutes(1), true, null));
            await session.PublishAsync(new("Total", TagRoleEnum.TotalCounter, 3410m, oldCounterAt, true, null));
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.None));
            services.AddSingleton<IClock>(clock);
            services.AddOeeInfrastructure(connection.ConnectionString, includeSignInManager: false, includeIdentityStore: false);
            services.AddSingleton<IOpcUaClientAdapter>(fake);
            services.AddSingleton<IOptions<OpcUaWorkerOptions>>(Options.Create(new OpcUaWorkerOptions { ReadRefreshSeconds = 1 }));
            services.AddSingleton<OpcUaIngestionWorker>();
            await using var provider = services.BuildServiceProvider();
            var worker = provider.GetRequiredService<OpcUaIngestionWorker>();
            await worker.StartAsync(CancellationToken.None);
            try
            {
                await WaitUntilAsync(() => db.CounterCheckpoints.AsNoTracking().AnyAsync(x => x.LastTotalCount == 3410));
                var previousOutput = await db.ProductionOutputs.AsNoTracking().SingleAsync();
                Assert.Equal(oldPlan.Id, previousOutput.ProductionPlanId);
                Assert.Equal(oldCounterAt, previousOutput.PeriodEnd);
                Assert.Equal(37m, previousOutput.TotalProducedQuantity);
                var live = new LiveOeeQueryService(db, clock);
                Assert.Equal(0m, Assert.Single((await live.GetAsync(new(), default)).Machines).ProductionPeriods!.ShiftQuantity);

                await session.PublishAsync(new("Total", TagRoleEnum.TotalCounter, 3427m, now, true, null));
                await WaitUntilAsync(() => db.CounterCheckpoints.AsNoTracking().AnyAsync(x => x.LastTotalCount == 3427));
                var machine = Assert.Single((await live.GetAsync(new(), default)).Machines);
                Assert.Equal("NIGHT", machine.CurrentPlan);
                Assert.Equal(17m, machine.ProductionPeriods!.ShiftQuantity);
                Assert.Equal(17m, machine.ProductionPeriods.CurrentProductQuantity);
                Assert.Equal(54m, machine.ProductionPeriods.DayQuantity); // 37 old + 17 new, no loss or duplicate.
                Assert.Equal(17m, await db.ProductionOutputs.Where(x => x.ProductionPlanId == newPlan.Id).SumAsync(x => x.TotalProducedQuantity));
                Assert.Empty(await db.ProductionReconciliationItems.ToArrayAsync());

                // Hai frame có thể có cùng OccurredAt khi Run đang đi trước Total.
                var rawPayloads = await db.OutboxMessages.AsNoTracking().Select(x => x.Payload).ToArrayAsync();
                var oldRaw = Assert.Single(rawPayloads.Select(x => JsonSerializer.Deserialize<DeviceTelemetryFrame>(x)), x => x?.TotalCounter == 3410m);
                Assert.NotNull(oldRaw);
                Assert.Equal(oldCounterAt, oldRaw.TotalCounterTimestamp);
                var snapshotId = await ApplicationServiceFactory.Snapshots(db, clock).RecalculateAsync(unit.Id, OeePeriodTypeEnum.Shift, "SHIFT-S03-20261015", boundary, boundary.AddHours(8), default);
                Assert.Equal(17m, await db.OeeSnapshots.Where(x => x.Id == snapshotId).Select(x => x.TotalQuantity).SingleAsync());
            }
            finally { await worker.StopAsync(CancellationToken.None); worker.Dispose(); }
        }
        finally { await db.Database.EnsureDeletedAsync(); SqlConnection.ClearAllPools(); }
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        while (!await predicate()) await Task.Delay(100, timeout.Token);
    }

    private sealed class TestClock(DateTimeOffset at) : IClock { public DateTimeOffset UtcNow => at; }
}
