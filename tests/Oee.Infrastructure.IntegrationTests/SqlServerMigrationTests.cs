using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Oee.Application.Common;
using Oee.Application.Contracts.Modules;
using Oee.Application.PlcIntegration;
using Oee.Application.Security;
using Oee.Domain.Common;
using Oee.Domain.Downtime;
using Oee.Domain.Entities.Organization;
using Oee.Domain.Entities.Production;
using Oee.Domain.Platform;
using Oee.Domain.PlcIntegration;
using Oee.Infrastructure.Audit;
using Oee.Infrastructure.Operations;
using Oee.Infrastructure.Persistence;
using Oee.Tests.Fixtures;

namespace Oee.Infrastructure.IntegrationTests;

/// <summary>
/// Integration Test sâu cho migration và các constraint chỉ SQL Server mới mô phỏng chính xác.
/// </summary>
/// <remarks>
/// EF InMemory không kiểm tra được filtered unique index, check constraint, foreign key, RowVersion hay rollback script,
/// nên test này chỉ chạy khi OEE_TEST_SQLSERVER được cấu hình và luôn dùng database tách biệt.
/// </remarks>
public sealed class SqlServerMigrationTests
{
    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task InitialMigrationCreatesRelationalConstraints()
    {
        // Arrange: tạo tên database test có prefix cố định để cleanup không thể nhắm nhầm OeeDb.
        var baseConnectionString = Environment.GetEnvironmentVariable("OEE_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(baseConnectionString))
        {
            return;
        }

        var databaseName = $"OeeDb_Phase2Tests_{Guid.NewGuid():N}";
        var connection = new SqlConnectionStringBuilder(baseConnectionString) { InitialCatalog = databaseName };
        Assert.StartsWith("OeeDb_Phase2Tests_", connection.InitialCatalog, StringComparison.Ordinal);

        var options = new DbContextOptionsBuilder<OeeDbContext>()
            .UseSqlServer(connection.ConnectionString)
            .AddInterceptors(new AuditSaveChangesInterceptor(new TestActor()))
            .Options;

        await using var dbContext = new OeeDbContext(options);
        try
        {
            // Act & Assert: áp dụng toàn bộ migration và xác nhận các migration nghiệp vụ quan trọng đã có.
            await dbContext.Database.MigrateAsync();
            var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();
            Assert.Equal(dbContext.Database.GetMigrations(), appliedMigrations);
            Assert.Contains(appliedMigrations, migration => migration.EndsWith("AddIdentitySecurity", StringComparison.Ordinal));
            Assert.Contains(appliedMigrations, migration => migration.EndsWith("AddProductionPlanVersioning", StringComparison.Ordinal));
            Assert.Contains(appliedMigrations, migration => migration.EndsWith("AddOpcUaResilience", StringComparison.Ordinal));
            Assert.Contains(appliedMigrations, migration => migration.EndsWith("AddProductionPlanSampleTraceability", StringComparison.Ordinal));
            Assert.Contains(appliedMigrations, migration => migration.EndsWith("AddAuditRetentionRunHistory", StringComparison.Ordinal));
            Assert.Contains(appliedMigrations, migration => migration.EndsWith("OrganizeOeeSchemas", StringComparison.Ordinal));
            Assert.Contains(appliedMigrations, migration => migration.EndsWith("RenameEquipmentHierarchyToAreaWorkCenter", StringComparison.Ordinal));

            // Assert cấu trúc: sys.tables phải chứa tối thiểu số bảng mong đợi sau migration.
            var tableCount = await dbContext.Database
                .SqlQueryRaw<int>("SELECT COUNT(*) AS [Value] FROM sys.tables")
                .SingleAsync();
            Assert.True(tableCount >= 36);

            var tablesBySchema = await dbContext.Database
                .SqlQueryRaw<string>("""
                    SELECT TABLE_SCHEMA + '.' + TABLE_NAME AS [Value]
                    FROM INFORMATION_SCHEMA.TABLES
                    WHERE TABLE_TYPE = 'BASE TABLE'
                    """)
                .ToArrayAsync();
            Assert.Contains("identity.Users", tablesBySchema);
            Assert.Contains("org.Sites", tablesBySchema);
            Assert.Contains("org.Areas", tablesBySchema);
            Assert.Contains("org.WorkCenters", tablesBySchema);
            Assert.DoesNotContain("org.Processes", tablesBySchema);
            Assert.DoesNotContain("org.WorkUnitGroups", tablesBySchema);
            Assert.Contains("catalog.Products", tablesBySchema);
            Assert.Contains("planning.ProductionPlans", tablesBySchema);
            Assert.Contains("production.ProductionOutputs", tablesBySchema);
            Assert.Contains("downtime.DowntimeEvents", tablesBySchema);
            Assert.Contains("oee.OeeSnapshots", tablesBySchema);
            Assert.Contains("plc.PlcDeviceMappings", tablesBySchema);
            Assert.Contains("audit.AuditLogs", tablesBySchema);
            Assert.Contains("integration.OutboxMessages", tablesBySchema);
            Assert.DoesNotContain("dbo.Users", tablesBySchema);
            Assert.DoesNotContain("dbo.Sites", tablesBySchema);

            // Arrange: seed cây tổ chức/master qua EF Core để kiểm tra foreign key và audit interceptor.
            var enterprise = DomainTestData.Create<EnterpriseEntity>(("Code", "E-TEST"), ("Name", "Test"));
            var site = DomainTestData.Create<SiteEntity>(("Enterprise", enterprise), ("Code", "S-TEST"), ("Name", "Test"), ("TimeZoneId", "UTC"));
            var area = DomainTestData.Create<AreaEntity>(("Site", site), ("Code", "P-TEST"), ("Name", "Test"));
            var group = DomainTestData.Create<WorkCenterEntity>(("Area", area), ("Code", "G-TEST"), ("Name", "Test"));
            var workUnit = DomainTestData.Create<WorkUnitEntity>(("WorkCenter", group), ("Code", "W-TEST"), ("Name", "Test"));
            var product = DomainTestData.Create<ProductEntity>(("Code", "SKU-TEST"), ("Name", "Test"), ("DefaultUom", "unit"));
            var reason = DomainTestData.Create<DowntimeReasonEntity>(("Code", "DR-TEST"), ("Name", "Test"), ("Category", DowntimeCategoryEnum.AvailabilityLoss), ("Kind", DowntimeKindEnum.Breakdown));
            dbContext.AddRange(enterprise, site, area, group, workUnit, product, reason);
            await dbContext.SaveChangesAsync();

            // Act & Assert: query dashboard hoạt động trên schema mới khi chưa có output/snapshot.
            var dashboardQuery = new DashboardQueryService(dbContext, new TestClock());
            var dashboardFilters = await dashboardQuery.GetFilterOptionsAsync(CancellationToken.None);
            var dashboard = await dashboardQuery.GetAsync(
                new DashboardQueryRequest(dashboardFilters.Today, dashboardFilters.Today, SiteId: site.Id),
                CancellationToken.None);
            Assert.Contains(dashboardFilters.Sites, item => item.Id == site.Id);
            Assert.False(dashboard.HasData);

            // Assert: audit JSON hợp lệ, IP được normalize và append-only guard chặn tampering.
            var productAudit = await dbContext.AuditLogs.AsNoTracking()
                .SingleAsync(log => log.EntityType == "Product" && log.EntitySubject == "SKU-TEST");
            Assert.Equal("127.0.0.1", productAudit.IpAddress);
            Assert.Equal("security-test", productAudit.UserName);
            Assert.StartsWith("{", productAudit.Changes, StringComparison.Ordinal);
            dbContext.ChangeTracker.Clear();
            DomainTestData.Set(productAudit, "Action", "Tamper");
            dbContext.Update(productAudit);
            Assert.Throws<InvalidOperationException>(() => dbContext.SaveChanges());
            dbContext.Entry(productAudit).State = EntityState.Detached;

            // Act & Assert: database trigger/constraint chặn khoảng định mức chồng lấn.
            dbContext.Add(DomainTestData.Create<WorkUnitStandardEntity>(("WorkUnitId", workUnit.Id), ("ProductId", product.Id), ("StandardSpeedPerMinute", 10), ("Uom", "unit"), ("EffectiveFrom", new DateOnly(2026, 1, 1))));
            await dbContext.SaveChangesAsync();
            var overlappingStandard = DomainTestData.Create<WorkUnitStandardEntity>(("WorkUnitId", workUnit.Id), ("ProductId", product.Id), ("StandardSpeedPerMinute", 11), ("Uom", "unit"), ("EffectiveFrom", new DateOnly(2026, 6, 1)));
            dbContext.Add(overlappingStandard);
            await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
            dbContext.Entry(overlappingStandard).State = EntityState.Detached;

            // Act & Assert: filtered unique index chỉ cho một downtime EndAt null trên mỗi WorkUnit.
            dbContext.Add(DomainTestData.Create<DowntimeEventEntity>(("WorkUnitId", workUnit.Id), ("DowntimeReasonId", reason.Id), ("Category", reason.Category), ("Kind", reason.Kind), ("Source", DataSourceEnum.Manual), ("StartAt", DateTimeOffset.UtcNow.AddMinutes(-5))));
            await dbContext.SaveChangesAsync();
            var secondOpenEvent = DomainTestData.Create<DowntimeEventEntity>(("WorkUnitId", workUnit.Id), ("DowntimeReasonId", reason.Id), ("Category", reason.Category), ("Kind", reason.Kind), ("Source", DataSourceEnum.Manual), ("StartAt", DateTimeOffset.UtcNow));
            dbContext.Add(secondOpenEvent);
            await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
            dbContext.Entry(secondOpenEvent).State = EntityState.Detached;

            // Act & Assert: check constraint ISJSON từ chối Changes không phải JSON.
            var invalidAudit = DomainTestData.Create<AuditLogEntity>(("Timestamp", DateTimeOffset.UtcNow), ("Action", "Test"), ("EntityType", "Test"), ("EntityId", "1"), ("Changes", "not-json"));
            dbContext.Add(invalidAudit);
            await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
            dbContext.Entry(invalidAudit).State = EntityState.Detached;

            // Act & Assert: automatic output thiếu ProductionPlan bị database chặn theo strict production mode.
            var automaticOutputWithoutPlan = DomainTestData.Create<ProductionOutputEntity>(("WorkUnitId", workUnit.Id), ("ProductId", product.Id), ("PeriodStart", DateTimeOffset.UtcNow.AddMinutes(-1)), ("PeriodEnd", DateTimeOffset.UtcNow), ("StandardSpeedPerMinute", 10), ("Source", DataSourceEnum.Automatic));
            dbContext.Add(automaticOutputWithoutPlan);
            await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
            dbContext.Entry(automaticOutputWithoutPlan).State = EntityState.Detached;

            // Arrange & Act: telemetry fault 0 không tạo downtime; delta thiếu active plan đi vào reconciliation/outbox.
            var connectionConfig = DomainTestData.Create<OpcUaConnectionEntity>(("Code", "OPC-TEST"), ("Name", "Test"), ("EndpointUrl", "opc.tcp://fake"), ("SecurityMode", "None"), ("SecurityPolicy", "None"), ("IsActive", true));
            var device = DomainTestData.Create<PlcDeviceMappingEntity>(("OpcUaConnection", connectionConfig), ("WorkUnitId", workUnit.Id), ("ExternalDeviceId", "PLC-TEST"), ("CounterMode", CounterModeEnum.Cumulative), ("FaultCodeFormat", FaultCodeFormatEnum.Auto), ("IsActive", true));
            dbContext.Add(device);
            await dbContext.SaveChangesAsync();
            var processor = ApplicationServiceFactory.Telemetry(dbContext, new TestClock());
            var sampleAt = DateTimeOffset.UtcNow;
            var downtimeCountBeforeZeroFault = await dbContext.DowntimeEvents.CountAsync(x => x.WorkUnitId == workUnit.Id);
            var initialFrame = new DeviceTelemetryFrame(device.Id, true, 100, 2, "0", null, sampleAt, "sample-1");
            Assert.True((await processor.ProcessAsync(initialFrame, CancellationToken.None)).Accepted);
            Assert.Equal(downtimeCountBeforeZeroFault, await dbContext.DowntimeEvents.CountAsync(x => x.WorkUnitId == workUnit.Id));
            Assert.Empty(await dbContext.PlcIntegrationEvents.Where(x => x.PlcDeviceMappingId == device.Id && x.Kind == "UnclassifiedFault").ToArrayAsync());
            var deltaFrame = initialFrame with { TotalCounter = 105, DefectCounter = 3, SourceTimestamp = sampleAt.AddSeconds(1), SampleKey = "sample-2" };
            var quarantined = await processor.ProcessAsync(deltaFrame, CancellationToken.None);
            Assert.True(quarantined.Quarantined);
            Assert.Equal(5, quarantined.TotalDelta);
            Assert.Single(await dbContext.ProductionReconciliationItems.Where(x => x.SourceKey == "sample-2").ToArrayAsync());
            var restartedProcessor = ApplicationServiceFactory.Telemetry(dbContext, new TestClock());
            Assert.False((await restartedProcessor.ProcessAsync(deltaFrame, CancellationToken.None)).Accepted);
            Assert.Equal(2, await dbContext.OutboxMessages.CountAsync(x => x.Type == "PlcTelemetryFrame"));

            // Act & Assert: EF có thể sinh script rollback để kế hoạch triển khai đánh giá trước khi dùng.
            var migrationList = appliedMigrations.ToArray();
            var rollbackScript = dbContext.GetService<IMigrator>().GenerateScript(
                migrationList[^1], Migration.InitialDatabase, MigrationsSqlGenerationOptions.Idempotent);
            Assert.Contains("DROP TABLE", rollbackScript, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            // Cleanup database test tách biệt và giải phóng connection pool trước lượt sau.
            await dbContext.Database.EnsureDeletedAsync();
            SqlConnection.ClearAllPools();
        }
    }

    private sealed class TestActor : ICurrentActor
    {
        public string? UserId => Guid.Empty.ToString();
        public string UserName => "security-test";
        public string? IpAddress => "::ffff:127.0.0.1";
    }

    private sealed class TestClock : IClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
}
