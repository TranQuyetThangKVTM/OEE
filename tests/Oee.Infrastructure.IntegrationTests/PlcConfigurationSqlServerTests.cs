using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Oee.Application.Common;
using Oee.Application.Contracts.PlcIntegration;
using Oee.Application.Security;
using Oee.Domain.Common;
using Oee.Domain.Entities.Organization;
using Oee.Domain.PlcIntegration;
using Oee.Infrastructure.Audit;
using Oee.Infrastructure.Integrations.OpcUa;
using Oee.Infrastructure.Persistence;
using Oee.Infrastructure.PlcIntegration;
using Oee.Tests.Fixtures;

namespace Oee.Infrastructure.IntegrationTests;

/// <summary>
/// Integration Test cho cấu hình OPC UA/PLC, fault mapping, audit và nguồn event live/persisted.
/// </summary>
public sealed class PlcConfigurationSqlServerTests
{
    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task ConfigurationWorkflowIsConcurrentAuditedAndSecretSafe()
    {
        // Arrange: tạo database SQL Server riêng; bỏ qua khi môi trường test chưa cấp connection string.
        var baseConnectionString = Environment.GetEnvironmentVariable("OEE_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(baseConnectionString)) return;

        var databaseName = $"OeeDb_PlcTests_{Guid.NewGuid():N}";
        var connection = new SqlConnectionStringBuilder(baseConnectionString) { InitialCatalog = databaseName };
        Assert.StartsWith("OeeDb_PlcTests_", connection.InitialCatalog, StringComparison.Ordinal);
        var queries = new QueryCounter();
        var options = new DbContextOptionsBuilder<OeeDbContext>().UseSqlServer(connection.ConnectionString)
            .AddInterceptors(new AuditSaveChangesInterceptor(new TestActor()), queries).Options;
        await using var db = new OeeDbContext(options);
        try
        {
            // Arrange: migrate và seed cây thiết bị cùng lookup Product/DowntimeReason.
            await db.Database.MigrateAsync();
            var enterprise = DomainTestData.Create<EnterpriseEntity>(("Code", "E-PLC"), ("Name", "Enterprise"));
            var site = DomainTestData.Create<SiteEntity>(("Enterprise", enterprise), ("Code", "S-PLC"), ("Name", "Site"), ("TimeZoneId", "Asia/Ho_Chi_Minh"));
            var area = DomainTestData.Create<AreaEntity>(("Site", site), ("Code", "P-PLC"), ("Name", "Area"));
            var group = DomainTestData.Create<WorkCenterEntity>(("Area", area), ("Code", "G-PLC"), ("Name", "Group"));
            var workUnit = DomainTestData.Create<WorkUnitEntity>(("WorkCenter", group), ("Code", "M-PLC"), ("Name", "Machine"));
            var product = DomainTestData.Create<ProductEntity>(("Code", "SKU-PLC"), ("Name", "Product"), ("DefaultUom", "bottle"));
            var reason = DomainTestData.Create<DowntimeReasonEntity>(("Code", "DR-PLC"), ("Name", "Failure"), ("Category", DowntimeCategoryEnum.AvailabilityLoss), ("Kind", DowntimeKindEnum.Breakdown));
            db.AddRange(enterprise, site, area, group, workUnit, product, reason);
            await db.SaveChangesAsync();

            queries.Commands.Clear();
            var emptyBanner = await new OpcUiStatusService(db).GetAsync(CancellationToken.None);
            Assert.Equal(0, emptyBanner.Configured);
            Assert.Equal(ConnectivityStateEnum.NotConfigured, emptyBanner.WorstState);
            Assert.Single(queries.Commands);

            // Act & Assert: credential chỉ lưu dưới dạng secret reference và không xuất hiện trong DTO/JSON trả về.
            var adapter = new FakeOpcUaAdapter();
            var service = ApplicationServiceFactory.PlcConfiguration(db, new TestClock(), adapter);
            await Assert.ThrowsAsync<ArgumentException>(() => service.CreateConnectionAsync(new("OPC-NO-SECRET", "Invalid",
                "opc.tcp://fake:4840", "None", "None", "UserName", "operator", null, false, null, true), CancellationToken.None));
            var connectionId = await service.CreateConnectionAsync(new("OPC-PLC", "Line OPC", "opc.tcp://fake:4840",
                "None", "None", "UserName", "operator", "OEE_TEST_OPC_SECRET", false, null, true), CancellationToken.None);
            var savedConnection = Assert.Single((await service.ConnectionsAsync(new(), CancellationToken.None)).Items);
            Assert.True(savedConnection.HasCredentialSecret);
            Assert.DoesNotContain("OEE_TEST_OPC_SECRET", System.Text.Json.JsonSerializer.Serialize(savedConnection), StringComparison.Ordinal);
            Assert.True((await service.TestConnectionAsync(connectionId, CancellationToken.None)).Success);
            await adapter.GetSession(connectionId).PublishAsync(new("ns=2;s=Run", TagRoleEnum.Running, true, DateTimeOffset.UtcNow, true, null));
            Assert.NotEmpty(await service.BrowseAsync(new(connectionId, null), CancellationToken.None));

            // Act & Assert: lưu mapping/tag động và tổng hợp đúng số tag Read/Write.
            var mappingId = await service.CreateDeviceConfigurationAsync(new(connectionId, workUnit.Id, product.Id,
                "PLC-M-001", "Cumulative", "Auto", true, 2, "integration", true,
                [new(null, "Running", "Read", "ns=2;s=Run", "BooleanValue", true),
                    new(null, "TotalCounter", "Read", "ns=2;s=Total", "DoublePrecision", true),
                    new(null, "FaultCode", "Read", "ns=2;s=Fault", "Text", false),
                    new(null, "Oee", "Write", "ns=2;s=Oee", "DoublePrecision", false)]), CancellationToken.None);
            var configuration = await service.DeviceConfigurationAsync(mappingId, CancellationToken.None);
            Assert.True(configuration.Mapping.ZeroFaultCodeMeansNoFault);
            Assert.Equal(3, configuration.Mapping.ReadTagCount);
            Assert.Equal(1, configuration.Mapping.WriteTagCount);

            // Paging projection conserve counts, defaults and version while avoiding five SQL round trips.
            queries.Commands.Clear();
            var page = await service.DeviceMappingsAsync(new(Search: "PLC-M", ScopeId: connectionId,
                SortBy: "workunit", PageSize: 1), CancellationToken.None);
            var row = Assert.Single(page.Items);
            Assert.Equal(1, page.TotalCount);
            Assert.Equal(configuration.Mapping, row);
            Assert.Equal("NotConfigured", row.Connectivity);
            Assert.Equal(2, queries.Commands.Count);
            Assert.DoesNotContain(queries.Commands, sql => sql.Contains("CredentialSecretReference", StringComparison.Ordinal));

            var lastRead = DateTimeOffset.UtcNow;
            db.CounterCheckpoints.Add(DomainTestData.Create<CounterCheckpointEntity>(("PlcDeviceMappingId", mappingId), ("LastReadAt", lastRead)));
            db.PlcDeviceRuntimeStates.Add(DomainTestData.Create<PlcDeviceRuntimeStateEntity>(("PlcDeviceMappingId", mappingId), ("ConnectivityState", ConnectivityStateEnum.Connected), ("LastDataAt", lastRead.AddSeconds(30))));
            await db.SaveChangesAsync();
            queries.Commands.Clear();
            row = Assert.Single((await service.DeviceMappingsAsync(new(SortBy: "lastread", Descending: true), CancellationToken.None)).Items);
            Assert.Equal(lastRead, row.LastReadAt);
            Assert.Equal(lastRead.AddSeconds(30), row.LastReceivedAt);
            Assert.Equal("Connected", row.Connectivity);
            Assert.Equal(3, row.ReadTagCount);
            Assert.Equal(1, row.WriteTagCount);
            Assert.Equal(2, queries.Commands.Count);

            queries.Commands.Clear();
            var banner = await new OpcUiStatusService(db).GetAsync(CancellationToken.None);
            Assert.Equal(1, banner.Configured);
            Assert.Equal(1, banner.Connected);
            Assert.Equal(0, banner.Unhealthy);
            Assert.Equal(ConnectivityStateEnum.Connected, banner.WorstState);
            Assert.Single(queries.Commands);

            var emptyPage = await service.DeviceMappingsAsync(new(Page: 2, PageSize: 1), CancellationToken.None);
            Assert.Empty(emptyPage.Items);
            Assert.Equal(1, emptyPage.TotalCount);
            Assert.Empty((await service.DeviceMappingsAsync(new(State: "false"), CancellationToken.None)).Items);

            // Runtime của mapping tạm dừng không được làm banner chung báo lỗi cho mọi trang.
            var runtimeState = await db.PlcDeviceRuntimeStates.SingleAsync(x => x.PlcDeviceMappingId == mappingId);
            var mapping = await db.PlcDeviceMappings.SingleAsync(x => x.Id == mappingId);
            DomainTestData.Set(runtimeState, "ConnectivityState", ConnectivityStateEnum.BadTag);
            await db.SaveChangesAsync();
            Assert.Equal(ConnectivityStateEnum.BadTag, (await new OpcUiStatusService(db).GetAsync(CancellationToken.None)).WorstState);
            DomainTestData.Set(mapping, "IsActive", false);
            await db.SaveChangesAsync();
            queries.Commands.Clear();
            var pausedBanner = await new OpcUiStatusService(db).GetAsync(CancellationToken.None);
            Assert.Equal(0, pausedBanner.Configured);
            Assert.Equal(0, pausedBanner.Unhealthy);
            Assert.Equal(ConnectivityStateEnum.NotConfigured, pausedBanner.WorstState);
            Assert.Single(queries.Commands);
            DomainTestData.Set(mapping, "IsActive", true);
            DomainTestData.Set(runtimeState, "ConnectivityState", ConnectivityStateEnum.Connected);
            await db.SaveChangesAsync();
            configuration = await service.DeviceConfigurationAsync(mappingId, CancellationToken.None);

            // Act & Assert: update lần hai bằng RowVersion cũ phải nhận concurrency conflict.
            var staleVersion = configuration.Mapping.RowVersion;
            await adapter.GetSession(connectionId).PublishAsync(new("ns=2;s=Total", TagRoleEnum.TotalCounter, 123m, DateTimeOffset.UtcNow, true, null));
            var updated = new PlcDeviceConfigurationRequest(connectionId, workUnit.Id, product.Id, "PLC-M-001",
                "ResetPerShift", "Hex", true, 3, "updated", true, configuration.Tags, staleVersion);
            await service.UpdateDeviceConfigurationAsync(mappingId, updated, CancellationToken.None);
            Assert.Null(await db.CounterCheckpoints.Select(x => x.LastTotalCount).SingleAsync());
            Assert.Equal(lastRead, await db.CounterCheckpoints.Select(x => x.LastReadAt).SingleAsync());
            Assert.DoesNotContain(await db.PlcIntegrationEvents.ToArrayAsync(), x => x.Kind == "CounterRebased");
            await Assert.ThrowsAsync<ConcurrencyConflictException>(() => service.UpdateDeviceConfigurationAsync(mappingId, updated, CancellationToken.None));
            db.ChangeTracker.Clear();

            // Act & Assert: fault code cùng scope được chuẩn hóa và chặn trùng ở service/database.
            var faultId = await service.CreateFaultMappingAsync(new(workUnit.Id, "16#001A", reason.Id, "Motor trip", true), CancellationToken.None);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateFaultMappingAsync(new(workUnit.Id, "16#001A", reason.Id, null, true), CancellationToken.None));
            var fault = (await service.FaultMappingsAsync(new(Search: "001A", ScopeId: workUnit.Id), CancellationToken.None)).Items.Single();
            Assert.Equal(faultId, fault.Id);

            Assert.Contains(await db.AuditLogs.AsNoTracking().ToArrayAsync(), x => x.EntityType == "OpcUaConnection");
            Assert.Contains(await db.AuditLogs.AsNoTracking().ToArrayAsync(), x => x.EntityType == "PlcDeviceMapping");
            Assert.Contains(await db.AuditLogs.AsNoTracking().ToArrayAsync(), x => x.EntityType == "PlcFaultCodeMapping");
        }
        finally
        {
            // Cleanup database test, không tác động cấu hình PLC development/production.
            await db.Database.EnsureDeletedAsync();
            SqlConnection.ClearAllPools();
        }
    }

    private sealed class QueryCounter : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class TestActor : ICurrentActor
    {
        public string? UserId => Guid.Empty.ToString();
        public string UserName => "plc-test";
        public string? IpAddress => "127.0.0.1";
    }
    private sealed class TestClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
}
