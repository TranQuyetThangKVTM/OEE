using System.Net;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
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
using Oee.Infrastructure.Persistence;
using Oee.Tests.Fixtures;

namespace Oee.Infrastructure.IntegrationTests;

public sealed class WebBackgroundProcessingTests
{
    [Theory]
    [InlineData(60, 48, true)]
    [InlineData(0, 48, false)]
    [InlineData(60, 0, false)]
    public void SnapshotOptionsValidateActiveSettingsAndIgnoreRetiredMergeSettings(
        int seconds, int lookbackHours, bool valid)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BackgroundProcessing:Enabled"] = "false",
            ["OeeProcessing:SnapshotSeconds"] = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["OeeProcessing:SnapshotLookbackHours"] = lookbackHours.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["OeeProcessing:MergeEveryMinutes"] = "0",
            ["OeeProcessing:MergeLookbackHours"] = "0",
        }).Build();
        var services = new ServiceCollection().AddLogging().AddOeeBackgroundProcessing(config);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<OeeProcessingOptions>>();

        if (!valid)
        {
            Assert.Throws<OptionsValidationException>(() => options.Value);
            return;
        }

        Assert.Equal(seconds, options.Value.SnapshotSeconds);
        Assert.Equal(lookbackHours, options.Value.SnapshotLookbackHours);
    }

    [Theory]
    [InlineData(true, true, false, true, true)]
    [InlineData(true, false, true, true, false)]
    [InlineData(false, true, true, true, false)]
    [InlineData(true, true, true, false, false)]
    [InlineData(true, true, true, true, true)]
    public void ConfigurationControlsDatabaseProcessingPlcAndWriteBack(
        bool enabled, bool opcEnabled, bool writeBack, bool hasSql, bool expectsPlc)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BackgroundProcessing:Enabled"] = enabled.ToString(),
            ["ConnectionStrings:DefaultConnection"] = hasSql ? "Server=test;Database=test;Integrated Security=true" : "",
            ["OpcUa:Enabled"] = opcEnabled.ToString(),
            ["OpcUa:ReconnectDelaySeconds"] = "10",
            ["OpcUaWorker:Adapter"] = "Fake",
            ["OpcUaWorker:WriteBackEnabled"] = writeBack.ToString(),
        }).Build();
        var services = new ServiceCollection().AddLogging().AddOeeBackgroundProcessing(config);
        using var provider = services.BuildServiceProvider();
        var processing = provider.GetRequiredService<IOptions<BackgroundProcessingOptions>>().Value;
        var opc = provider.GetRequiredService<IOptions<OpcUaWorkerOptions>>().Value;
        Assert.Equal(enabled, processing.Enabled);
        if (opcEnabled) { Assert.Equal("Sdk", opc.Adapter); Assert.Equal(10, opc.InitialReconnectSeconds); }
        var hostedTypes = services.Where(x => x.ServiceType == typeof(IHostedService)).Select(x => x.ImplementationType).ToArray();
        Assert.Equal(expectsPlc, hostedTypes.Contains(typeof(OpcUaIngestionWorker)));
        Assert.Equal(enabled && hasSql, hostedTypes.Contains(typeof(OeeSnapshotWorker)));
        Assert.Equal(expectsPlc && writeBack, hostedTypes.Contains(typeof(OpcUaWriteBackWorker)));
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task WebHostCollectsBeforeFirstRequestAndAfterLastClientCloses()
    {
        var baseConnection = Environment.GetEnvironmentVariable("OEE_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(baseConnection)) return;
        var connection = new SqlConnectionStringBuilder(baseConnection)
        {
            InitialCatalog = $"OeeDb_WebHostTests_{Guid.NewGuid():N}",
        };
        Assert.StartsWith("OeeDb_WebHostTests_", connection.InitialCatalog, StringComparison.Ordinal);
        var options = new DbContextOptionsBuilder<OeeDbContext>().UseSqlServer(connection.ConnectionString).Options;
        await using var db = new OeeDbContext(options);
        try
        {
            await db.Database.MigrateAsync();
            var enterprise = DomainTestData.Create<EnterpriseEntity>(("Code", "E-WEB"), ("Name", "Test"));
            var site = DomainTestData.Create<SiteEntity>(("Enterprise", enterprise), ("Code", "S-WEB"), ("Name", "Test"), ("TimeZoneId", "UTC"));
            var area = DomainTestData.Create<AreaEntity>(("Site", site), ("Code", "A-WEB"), ("Name", "Test"));
            var center = DomainTestData.Create<WorkCenterEntity>(("Area", area), ("Code", "C-WEB"), ("Name", "Test"));
            var unit = DomainTestData.Create<WorkUnitEntity>(("WorkCenter", center), ("Code", "U-WEB"), ("Name", "Test"));
            var product = DomainTestData.Create<ProductEntity>(("Code", "SKU-WEB"), ("Name", "Test"), ("DefaultUom", "unit"));
            var plan = DomainTestData.Create<ProductionPlanEntity>(("WorkUnit", unit), ("Product", product), ("Code", "PLAN-WEB"), ("PlannedStart", DateTimeOffset.UtcNow.AddHours(-1)), ("PlannedEnd", DateTimeOffset.UtcNow.AddHours(1)), ("PlannedQuantity", 100));
            var opc = DomainTestData.Create<OpcUaConnectionEntity>(("Code", "OPC-WEB"), ("Name", "Test"), ("EndpointUrl", "opc.tcp://fake:4840"), ("SecurityMode", "None"), ("SecurityPolicy", "None"), ("IsActive", true));
            var device = DomainTestData.Create<PlcDeviceMappingEntity>(("OpcUaConnection", opc), ("WorkUnit", unit), ("ExternalDeviceId", "WEB-TEST"), ("IsActive", true), ("CounterMode", CounterModeEnum.Cumulative));
            db.AddRange(enterprise, site, area, center, unit, product, plan, opc, device,
                DomainTestData.Create<DowntimeReasonEntity>(("Code", "DR-UNKNOWN"), ("Name", "Unclassified"), ("Category", DowntimeCategoryEnum.AvailabilityLoss), ("Kind", DowntimeKindEnum.Breakdown)),
                DomainTestData.Create<WorkUnitStandardEntity>(("WorkUnit", unit), ("Product", product), ("StandardSpeedPerMinute", 100), ("Uom", "unit"), ("EffectiveFrom", new DateOnly(2026, 1, 1))),
                DomainTestData.Create<OpcUaTagMappingEntity>(("PlcDeviceMapping", device), ("NodeId", "ns=2;s=Run"), ("TagRole", TagRoleEnum.Running), ("Direction", TagDirectionEnum.Read), ("DataType", OpcUaDataTypeEnum.BooleanValue), ("IsRequired", true)),
                DomainTestData.Create<OpcUaTagMappingEntity>(("PlcDeviceMapping", device), ("NodeId", "ns=2;s=Total"), ("TagRole", TagRoleEnum.TotalCounter), ("Direction", TagDirectionEnum.Read), ("DataType", OpcUaDataTypeEnum.DoublePrecision), ("IsRequired", true)));
            await db.SaveChangesAsync();
            var fake = new FakeOpcUaAdapter();
            var session = fake.GetSession(opc.Id);
            await session.WriteAsync([new("ns=2;s=Run", true), new("ns=2;s=Total", 10m)], CancellationToken.None);
            await using (var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = connection.ConnectionString,
                    ["BackgroundProcessing:Enabled"] = "true",
                    ["OpcUa:Enabled"] = "true",
                    ["OpcUa:EndpointUrl"] = "opc.tcp://fake:4840",
                    ["OpcUaWorker:WriteBackEnabled"] = "false",
                    ["OpcUaWorker:ConfigurationRefreshSeconds"] = "5",
                    ["OpcUaWorker:ReadRefreshSeconds"] = "1",
                    ["OpcUaWorker:NoDataSeconds"] = "5",
                    ["AuditRetention:Enabled"] = "false",
                }));
                builder.ConfigureServices(services =>
                {
                    // Minimal hosting đã đọc chuỗi SQL tại composition root trước callback test.
                    // Thay DbContextOptions để tất cả service chỉ chạm database test này.
                    services.RemoveAll<DbContextOptions<OeeDbContext>>();
                    services.AddDbContext<OeeDbContext>((provider, context) => context
                        .UseSqlServer(connection.ConnectionString)
                        .AddInterceptors(provider.GetServices<ISaveChangesInterceptor>()));
                    services.RemoveAll<IOpcUaClientAdapter>();
                    services.AddSingleton<IOpcUaClientAdapter>(fake);
                    // Cookie transport that does not require seeding an unrelated Identity user.
                    services.PostConfigure<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme,
                        cookie => cookie.Events.OnValidatePrincipal = _ => Task.CompletedTask);
                });
            }))
            {
                // Khởi động host mà không gửi request HTTP hoặc mở phiên trình duyệt.
                var hosted = factory.Services.GetServices<IHostedService>().ToArray();
                Assert.Single(hosted.OfType<OpcUaIngestionWorker>());
                Assert.Single(hosted.OfType<OeeSnapshotWorker>());
                Assert.Empty(hosted.OfType<OpcUaWriteBackWorker>());
                await WaitUntilAsync(async () => await db.CounterCheckpoints.AsNoTracking().AnyAsync(x => x.PlcDeviceMappingId == device.Id && x.LastTotalCount == 10));
                await session.PublishAsync(new("ns=2;s=Total", TagRoleEnum.TotalCounter, 18m, DateTimeOffset.UtcNow.AddMilliseconds(100), true, null));
                await WaitUntilAsync(async () => await db.CounterCheckpoints.AsNoTracking().AnyAsync(x => x.PlcDeviceMappingId == device.Id && x.LastTotalCount == 18));
                Assert.Empty(await db.ProductionReconciliationItems.ToArrayAsync());
                Assert.Equal(8m, await db.ProductionOutputs.SumAsync(x => x.TotalProducedQuantity));
                Assert.False(await db.OutboxMessages.AnyAsync(x => x.Type == "PlcTelemetryFrame" && x.ProcessedAt == null));
                Assert.Contains(await db.AuditLogs.AsNoTracking().ToArrayAsync(), x => x.UserName == "System" && x.EntityType == "ProductionOutput");
                using (var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }))
                {
                    using var login = await client.GetAsync("/Account/Login");
                    Assert.Equal(HttpStatusCode.OK, login.StatusCode);
                    var cookieOptions = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
                        .Get(IdentityConstants.ApplicationScheme);
                    var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "background-browser-test")], IdentityConstants.ApplicationScheme));
                    var cookie = cookieOptions.Cookie.Name + "=" + cookieOptions.TicketDataFormat.Protect(
                        new AuthenticationTicket(principal, new AuthenticationProperties(), IdentityConstants.ApplicationScheme));
                    var socketClient = factory.Server.CreateWebSocketClient();
                    socketClient.ConfigureRequest = request => request.Headers.Cookie = cookie;
                    using var socket = await socketClient.ConnectAsync(new Uri("ws://localhost/_blazor"), CancellationToken.None);
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("{\"protocol\":\"blazorpack\",\"version\":1}\u001e")), WebSocketMessageType.Text, true, timeout.Token);
                    var buffer = new byte[4096];
                    var handshake = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
                    Assert.StartsWith("{}\u001e", Encoding.UTF8.GetString(buffer, 0, handshake.Count), StringComparison.Ordinal);
                    socket.Abort(); // Đóng transport như khi tab bị đóng đột ngột.
                }
                // Client HTTP và WebSocket cuối đã đóng; không gửi thêm HTTP phía dưới.
                // Constant SourceTimestamp/value: polling refreshes reception health without producing duplicates.
                var sourceAt = await db.CounterCheckpoints.AsNoTracking().Select(x => x.LastReadAt).SingleAsync();
                var counterChangedAt = await db.CounterCheckpoints.AsNoTracking().Select(x => x.LastCounterChangedAt).SingleAsync();
                Assert.NotNull(counterChangedAt);
                var receptionAt = await db.PlcDeviceRuntimeStates.AsNoTracking().Select(x => x.LastDataAt).SingleAsync();
                await Task.Delay(TimeSpan.FromSeconds(6));
                var healthy = await db.PlcDeviceRuntimeStates.AsNoTracking().SingleAsync();
                Assert.Equal(ConnectivityStateEnum.Connected, healthy.ConnectivityState);
                Assert.True(healthy.LastDataAt > receptionAt);
                Assert.Equal(sourceAt, await db.CounterCheckpoints.AsNoTracking().Select(x => x.LastReadAt).SingleAsync());
                Assert.Equal(counterChangedAt, await db.CounterCheckpoints.AsNoTracking().Select(x => x.LastCounterChangedAt).SingleAsync());
                Assert.Empty(await db.ProductionReconciliationItems.ToArrayAsync());
                // Kế hoạch dùng ngay sau khi tạo, không có thao tác phát hành.
                await session.PublishAsync(new("ns=2;s=Total", TagRoleEnum.TotalCounter, 21m, DateTimeOffset.UtcNow, true, null));
                await WaitUntilAsync(async () => await db.CounterCheckpoints.AsNoTracking().AnyAsync(x => x.PlcDeviceMappingId == device.Id && x.LastTotalCount == 21));
                Assert.Equal(11m, await db.ProductionOutputs.SumAsync(x => x.TotalProducedQuantity));
                Assert.True(await db.CounterCheckpoints.AsNoTracking().Select(x => x.LastCounterChangedAt).SingleAsync() > counterChangedAt);
                Assert.Empty(await db.ProductionReconciliationItems.ToArrayAsync());

                // Chuyển cửa sổ kế hoạch khi không có client, nhận số tăng vào đúng kế hoạch mới.
                var boundary = DateTimeOffset.UtcNow.AddSeconds(3);
                DomainTestData.Set(plan, "PlannedEnd", boundary);
                var nextPlan = DomainTestData.Create<ProductionPlanEntity>(("WorkUnit", unit), ("Product", product),
                    ("Code", "PLAN-NEXT"), ("PlannedStart", boundary), ("PlannedEnd", boundary.AddHours(8)), ("PlannedQuantity", 100));
                DomainTestData.Set(nextPlan, "VersionSeriesId", nextPlan.Id);
                db.ProductionPlans.Add(nextPlan);
                await db.SaveChangesAsync();
                await WaitUntilAsync(() => Task.FromResult(DateTimeOffset.UtcNow > boundary));
                // Không phát notification: chỉ vòng read refresh nền phải nhận counter mới.
                await session.WriteAsync([new("ns=2;s=Total", 28m)], CancellationToken.None);
                await WaitUntilAsync(() => db.CounterCheckpoints.AsNoTracking().AnyAsync(x => x.LastTotalCount == 28));
                Assert.Equal(11m, await db.ProductionOutputs.Where(x => x.ProductionPlanId == plan.Id).SumAsync(x => x.TotalProducedQuantity));
                Assert.Equal(7m, await db.ProductionOutputs.Where(x => x.ProductionPlanId == nextPlan.Id).SumAsync(x => x.TotalProducedQuantity));
                Assert.Empty(await db.ProductionReconciliationItems.ToArrayAsync());
                Assert.True(session.IsConnected);
                Assert.False(hosted.OfType<OpcUaIngestionWorker>().Single().ExecuteTask!.IsCompleted);
                await AssertBackgroundDiagnosticSqlAsync(connection.ConnectionString);
                await session.PublishAsync(new("ns=2;s=Run", TagRoleEnum.Running, false, DateTimeOffset.UtcNow, true, null));
                await WaitUntilAsync(async () => await db.PlcDeviceRuntimeStates.AsNoTracking().AnyAsync(x => x.OperationalState == OperationalStateEnum.Stopped));
                Assert.Single(await db.DowntimeEvents.AsNoTracking().ToArrayAsync());
                Assert.Equal(18m, await db.ProductionOutputs.SumAsync(x => x.TotalProducedQuantity));
            }
            Assert.False(session.IsConnected);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
            SqlConnection.ClearAllPools();
        }
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> predicate)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (!await predicate()) await Task.Delay(100, timeout.Token);
    }

    private static async Task AssertBackgroundDiagnosticSqlAsync(string connectionString, [CallerFilePath] string sourceFile = "")
    {
        var sqlPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourceFile)!, "../../deployment/iis/Read-BackgroundActivity.sql"));
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        using var command = connection.CreateCommand();
        command.CommandText = await File.ReadAllTextAsync(sqlPath);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(await reader.NextResultAsync());
        Assert.True(await reader.ReadAsync());
        Assert.Equal("PLAN-NEXT", reader["CurrentPlan"]);
        Assert.Equal(7m, reader["RecordedTotal"]);
        Assert.Equal(28m, reader["LastTotalCount"]);
        Assert.True(await reader.NextResultAsync());
        Assert.False(await reader.ReadAsync());
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task MissingDatabaseDoesNotTerminateWebBackgroundHost()
    {
        var baseConnection = Environment.GetEnvironmentVariable("OEE_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(baseConnection)) return;
        var connection = new SqlConnectionStringBuilder(baseConnection)
        {
            InitialCatalog = $"OeeDb_WebHostMissing_{Guid.NewGuid():N}", ConnectTimeout = 1,
        };
        var ingestionLog = new FailureLogger<OpcUaIngestionWorker>();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BackgroundProcessing:Enabled"] = "true", ["OpcUa:Enabled"] = "true",
                ["OpcUa:EndpointUrl"] = "opc.tcp://fake:4840", ["AuditRetention:Enabled"] = "false",
                ["OpcUaWorker:WriteBackEnabled"] = "false",
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<OeeDbContext>>();
                services.AddDbContext<OeeDbContext>(context => context.UseSqlServer(connection.ConnectionString));
                services.AddSingleton<ILogger<OpcUaIngestionWorker>>(ingestionLog);
            });
        });
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await ingestionLog.Failed.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var hosted = factory.Services.GetServices<IHostedService>().OfType<BackgroundService>().ToArray();
        Assert.All(hosted, service => Assert.False(service.ExecuteTask?.IsFaulted));
        using var login = await client.GetAsync("/Account/Login");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    private sealed class FailureLogger<T> : ILogger<T>
    {
        public TaskCompletionSource Failed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Error && exception is not null) Failed.TrySetResult();
        }
    }

    private sealed class TestClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
}
