using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Oee.Application.PlcIntegration;
using Oee.Application.PlcIntegration.Monitoring;
using Oee.Application.Common;
using Oee.Domain.Common;
using Oee.Domain.Entities.Organization;
using Oee.Domain.Entities.Production;
using Oee.Domain.PlcIntegration;
using Oee.Infrastructure.BackgroundServices;
using Oee.Infrastructure.BackgroundServices.Configuration;
using Oee.Infrastructure.Integrations.OpcUa;
using Oee.Infrastructure.Persistence;
using Oee.Tests.Fixtures;

namespace Oee.Infrastructure.IntegrationTests;

public sealed class OpcUaResetSqlServerTests
{
    [SqlServerTheory]
    [InlineData(CounterModeEnum.Cumulative)]
    [InlineData(CounterModeEnum.ResetPerShift)]
    [InlineData(CounterModeEnum.ResetDaily)]
    [Trait("Category", "SqlServer")]
    public async Task ServerRestartWithTemporaryZeroDoesNotRecountHistoricalProduction(CounterModeEnum mode)
    {
        var baseConnection = Environment.GetEnvironmentVariable("OEE_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(baseConnection)) return;
        var connection = new SqlConnectionStringBuilder(baseConnection)
        {
            InitialCatalog = $"OeeDb_OpcResetTests_{Guid.NewGuid():N}",
        };
        Assert.StartsWith("OeeDb_OpcResetTests_", connection.InitialCatalog, StringComparison.Ordinal);
        var contextOptions = new DbContextOptionsBuilder<OeeDbContext>().UseSqlServer(connection.ConnectionString).Options;
        await using var db = new OeeDbContext(contextOptions);
        try
        {
            await db.Database.MigrateAsync();
            var at = DateTimeOffset.UtcNow.AddSeconds(-10);
            var enterprise = DomainTestData.Create<EnterpriseEntity>(("Code", "E"), ("Name", "Test"));
            var site = DomainTestData.Create<SiteEntity>(("Enterprise", enterprise), ("Code", "S"), ("Name", "Test"), ("TimeZoneId", "UTC"));
            var area = DomainTestData.Create<AreaEntity>(("Site", site), ("Code", "A"), ("Name", "Test"));
            var center = DomainTestData.Create<WorkCenterEntity>(("Area", area), ("Code", "C"), ("Name", "Test"));
            var unit = DomainTestData.Create<WorkUnitEntity>(("WorkCenter", center), ("Code", "M11"), ("Name", "Pallet robot"));
            var product = DomainTestData.Create<ProductEntity>(("Code", "SKU"), ("Name", "Test"), ("DefaultUom", "PCS"));
            var plan = DomainTestData.Create<ProductionPlanEntity>(("WorkUnit", unit), ("Product", product), ("Code", "PLAN"), ("PlannedStart", at.AddHours(-1)), ("PlannedEnd", at.AddHours(1)), ("PlannedQuantity", 100));
            DomainTestData.Set(plan, "VersionSeriesId", plan.Id);
            var opc = DomainTestData.Create<OpcUaConnectionEntity>(("Code", "OPC"), ("Name", "Test"), ("EndpointUrl", "opc.tcp://fake"), ("IsActive", true));
            var device = DomainTestData.Create<PlcDeviceMappingEntity>(("OpcUaConnection", opc), ("WorkUnit", unit), ("ExternalDeviceId", "M11"), ("IsActive", true), ("CounterMode", mode));
            db.AddRange(enterprise, site, area, center, unit, product, plan, opc, device,
                DomainTestData.Create<WorkUnitStandardEntity>(("WorkUnit", unit), ("Product", product), ("StandardSpeedPerMinute", .2m), ("Uom", "PCS"), ("EffectiveFrom", (global::System.DateOnly)(new(2026, 1, 1)))),
                DomainTestData.Create<CounterCheckpointEntity>(("PlcDeviceMapping", device), ("LastTotalCount", 2833), ("LastDefectCount", 2), ("LastReadAt", at.AddSeconds(-1)), ("LastSampleKey", "previous"), ("LastCounterChangedAt", at.AddMinutes(-5))),
                DomainTestData.Create<ProductionOutputEntity>(("ProductionPlan", plan), ("WorkUnit", unit), ("Product", product), ("PeriodStart", at.AddMinutes(-20)), ("PeriodEnd", at.AddMinutes(-1)), ("StandardSpeedPerMinute", .2m), ("TotalProducedQuantity", 4), ("Source", DataSourceEnum.Automatic)),
                DomainTestData.Create<OpcUaTagMappingEntity>(("PlcDeviceMapping", device), ("NodeId", "Run"), ("TagRole", TagRoleEnum.Running), ("Direction", TagDirectionEnum.Read), ("DataType", OpcUaDataTypeEnum.BooleanValue), ("IsRequired", true)),
                DomainTestData.Create<OpcUaTagMappingEntity>(("PlcDeviceMapping", device), ("NodeId", "Total"), ("TagRole", TagRoleEnum.TotalCounter), ("Direction", TagDirectionEnum.Read), ("DataType", OpcUaDataTypeEnum.DoublePrecision), ("IsRequired", true)),
                DomainTestData.Create<OpcUaTagMappingEntity>(("PlcDeviceMapping", device), ("NodeId", "Defect"), ("TagRole", TagRoleEnum.DefectCounter), ("Direction", TagDirectionEnum.Read), ("DataType", OpcUaDataTypeEnum.DoublePrecision)));
            await db.SaveChangesAsync();

            var first = new FakeOpcUaSession();
            var second = new FakeOpcUaSession();
            await SeedSessionAsync(first, 2833, at, 2);
            await SeedSessionAsync(second, 0, at.AddSeconds(1));
            var adapter = new RestartAdapter(first, second);
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.None));
            services.AddOeeInfrastructure(connection.ConnectionString, includeSignInManager: false, includeIdentityStore: false);
            services.AddSingleton<IOpcUaClientAdapter>(adapter);
            services.AddSingleton<IOptions<OpcUaWorkerOptions>>(Options.Create(new OpcUaWorkerOptions
            {
                InitialReconnectSeconds = 1, MaximumReconnectSeconds = 5, ConfigurationRefreshSeconds = 5,
                ReadRefreshSeconds = 1, NoDataSeconds = 5,
            }));
            services.AddSingleton<OpcUaIngestionWorker>();
            await using var provider = services.BuildServiceProvider();
            var worker = provider.GetRequiredService<OpcUaIngestionWorker>();
            var monitor = provider.GetRequiredService<IOpcTagMonitorBuffer>();
            await worker.StartAsync(CancellationToken.None);
            try
            {
                await WaitUntilAsync(() => db.CounterCheckpoints.AsNoTracking().AnyAsync(x => x.LastReadAt == at));
                Assert.Contains(monitor.Snapshot(DateTimeOffset.UtcNow).Values,
                    value => value.DeviceId == device.Id && value.Role == TagRoleEnum.TotalCounter && value.Value == "2833");
                await using (var scope = provider.CreateAsyncScope())
                {
                    var mappings = await scope.ServiceProvider.GetRequiredService<IUseCaseExecutor>()
                        .ExecuteAsync(new ListOpcTagMonitorMappingsQuery());
                    Assert.Equal(3, mappings.Count);
                    Assert.All(mappings, tag => Assert.Equal(device.Id, tag.DeviceId));
                }
                await first.DisposeAsync(); // OPC server restart, not a PLC counter reset.
                await WaitUntilAsync(() => db.PlcIntegrationEvents.AnyAsync(x => x.Kind == "CounterDiscontinuity"));
                Assert.Equal(2833m, await db.CounterCheckpoints.AsNoTracking().Select(x => x.LastTotalCount).SingleAsync());
                Assert.Equal(4m, await db.ProductionOutputs.SumAsync(x => x.TotalProducedQuantity));
                Assert.Contains(monitor.Snapshot(DateTimeOffset.UtcNow).Values,
                    value => value.DeviceId == device.Id && value.Role == TagRoleEnum.TotalCounter && value.Value == "0");

                // Repeated startup zeros must neither move the checkpoint nor flood the event log.
                await Task.Delay(1500);
                Assert.Single(await db.PlcIntegrationEvents.Where(x => x.Kind == "CounterDiscontinuity").ToArrayAsync());
                Assert.Equal(2833m, await db.CounterCheckpoints.AsNoTracking().Select(x => x.LastTotalCount).SingleAsync());
                Assert.True((await db.CounterCheckpoints.AsNoTracking().SingleAsync()).IsDiscontinuityPending);
                await second.PublishAsync(new("Total", TagRoleEnum.TotalCounter, 2861m, at.AddSeconds(2), true, null));
                await second.PublishAsync(new("Defect", TagRoleEnum.DefectCounter, 2m, at.AddSeconds(2), true, null));
                await WaitUntilAsync(() => db.CounterCheckpoints.AsNoTracking().AnyAsync(x => x.LastTotalCount == 2861));
                Assert.Equal(32m, await db.ProductionOutputs.SumAsync(x => x.TotalProducedQuantity)); // 4 + (2861 - 2833).
                Assert.False((await db.CounterCheckpoints.AsNoTracking().SingleAsync()).IsDiscontinuityPending);
                Assert.Contains(monitor.Snapshot(DateTimeOffset.UtcNow).Values,
                    value => value.DeviceId == device.Id && value.Role == TagRoleEnum.TotalCounter && value.Value == "2861");
                Assert.Equal(0, await db.CounterCheckpoints.AsNoTracking().Select(x => x.CounterEpoch).SingleAsync());
                Assert.Empty(await db.ProductionReconciliationItems.ToArrayAsync());

            }
            finally
            {
                await worker.StopAsync(CancellationToken.None);
                worker.Dispose();
            }
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
            SqlConnection.ClearAllPools();
        }
    }

    private static async Task SeedSessionAsync(FakeOpcUaSession session, decimal total, DateTimeOffset at, decimal defect = 0)
    {
        await session.PublishAsync(new("Run", TagRoleEnum.Running, true, at, true, null));
        await session.PublishAsync(new("Total", TagRoleEnum.TotalCounter, total, at, true, null));
        await session.PublishAsync(new("Defect", TagRoleEnum.DefectCounter, defect, at, true, null));
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        while (!await predicate()) await Task.Delay(100, timeout.Token);
    }

    private sealed class RestartAdapter(FakeOpcUaSession first, FakeOpcUaSession second) : IOpcUaClientAdapter
    {
        public Task<IOpcUaSession> ConnectAsync(OpcUaEndpointOptions endpoint, CancellationToken cancellationToken)
            => Task.FromResult<IOpcUaSession>(first.IsConnected ? first : second);
    }
}
