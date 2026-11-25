using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Oee.Application.Common;
using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.Operations;
using Oee.Application.Security;
using Oee.Domain.Common;
using Oee.Domain.Downtime;
using Oee.Domain.Entities.Oee;
using Oee.Domain.Entities.Organization;
using Oee.Domain.Entities.Production;
using Oee.Domain.PlcIntegration;
using Oee.Infrastructure.Audit;
using Oee.Infrastructure.Operations;
using Oee.Infrastructure.Persistence;
using Oee.Tests.Fixtures;

namespace Oee.Infrastructure.IntegrationTests;

/// <summary>
/// Integration Test trên SQL Server cho cảnh báo SLO, target có hiệu lực và màn hình OEE realtime.
/// </summary>
/// <remarks>
/// Test được bỏ qua khi biến môi trường OEE_TEST_SQLSERVER chưa được cấu hình; SQL Server thật cần thiết
/// để kiểm chứng migration, RowVersion/concurrency, audit cùng transaction và cách EF Core dịch truy vấn.
/// </remarks>
public sealed class AlertsRealtimeSqlServerTests
{
    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task AlertsTargetsAndRealtimeEnforceConcurrencyAuditAndConnectivitySemantics()
    {
        // Arrange: mỗi lượt test dùng database riêng để không can thiệp OeeDb development.
        var baseConnectionString = Environment.GetEnvironmentVariable("OEE_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(baseConnectionString)) return;

        var databaseName = $"OeeDb_AlertRealtimeTests_{Guid.NewGuid():N}";
        var connection = new SqlConnectionStringBuilder(baseConnectionString) { InitialCatalog = databaseName };
        Assert.StartsWith("OeeDb_AlertRealtimeTests_", connection.InitialCatalog, StringComparison.Ordinal);
        var actor = new TestActor();
        var options = new DbContextOptionsBuilder<OeeDbContext>()
            .UseSqlServer(connection.ConnectionString)
            .AddInterceptors(new AuditSaveChangesInterceptor(actor))
            .Options;
        await using var db = new OeeDbContext(options);
        try
        {
            // Arrange: áp dụng migration, seed hai thiết bị và tạo target/snapshot/cảnh báo thật trong SQL.
            await db.Database.MigrateAsync();
            var clock = new FixedClock();
            var seed = await SeedAsync(db, clock.UtcNow);
            var effective = ApplicationServiceFactory.EffectiveConfiguration(db);
            var targetId = await effective.AddTargetVersionAsync(new EffectiveTargetRequest(null, null,
                OeePeriodTypeContract.Day, .75m, .85m, .90m, .99m, new DateOnly(2026, 1, 1), null,
                "global baseline", "DAY"), CancellationToken.None);
            var target = await db.OeeTargets.SingleAsync(item => item.Id == targetId);
            var snapshot = CreateSnapshot(seed.OfflineUnit.Id, clock.UtcNow);
            db.OeeSnapshots.Add(snapshot);
            await db.SaveChangesAsync();
            var alert = DomainTestData.Create<OeeAlertEntity>(("SnapshotId", snapshot.Id), ("WorkUnitId", seed.OfflineUnit.Id), ("TargetId", target.Id), ("Metric", AlertMetricEnum.Oee), ("Severity", AlertSeverityEnum.Critical), ("State", AlertStateEnum.Open), ("ActualValue", .60m), ("TargetValue", .75m), ("TriggeredAt", clock.UtcNow.AddMinutes(-5)), ("CreatedAt", clock.UtcNow.AddMinutes(-5)));
            db.OeeAlerts.Add(alert);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            // Act & Assert: lọc cảnh báo, acknowledge/resolve và kiểm tra optimistic concurrency bằng token cũ.
            var snapshots = ApplicationServiceFactory.Snapshots(db, clock);
            var service = ApplicationServiceFactory.Alerts(db, snapshots, effective, clock, actor);
            var firstPage = await service.ListAsync(new AlertListQuery(State: "Open", Severity: "Critical",
                FromDate: new DateOnly(2026, 8, 1), ToDate: new DateOnly(2026, 8, 3), PageSize: 25), CancellationToken.None);
            var firstAlert = Assert.Single(firstPage.Items);
            Assert.Equal(-.15m, firstAlert.Difference);
            Assert.Equal("M-001", firstAlert.WorkUnit);

            await service.AcknowledgeAsync(firstAlert.Id, new(firstAlert.ConcurrencyToken, "operator accepted"), CancellationToken.None);
            await Assert.ThrowsAsync<ConcurrencyConflictException>(() => service.ResolveAsync(firstAlert.Id,
                new(firstAlert.ConcurrencyToken, "stale client"), CancellationToken.None));
            db.ChangeTracker.Clear();
            var acknowledged = Assert.Single((await service.ListAsync(new AlertListQuery(State: "Acknowledged"), CancellationToken.None)).Items);
            Assert.Equal("sql-alert-test", acknowledged.AcknowledgedBy);
            await service.ReevaluateAsync(acknowledged.Id, acknowledged.ConcurrencyToken, CancellationToken.None);
            db.ChangeTracker.Clear();
            var refreshed = Assert.Single((await service.ListAsync(new AlertListQuery(State: "Acknowledged"), CancellationToken.None)).Items);
            await service.ResolveAsync(refreshed.Id, new(refreshed.ConcurrencyToken, "fault cleared"), CancellationToken.None);
            db.ChangeTracker.Clear();
            Assert.Single((await service.ListAsync(new AlertListQuery(State: "Resolved"), CancellationToken.None)).Items);
            Assert.Contains(await db.AuditLogs.AsNoTracking().Where(log => log.EntityType == "OeeAlert").ToArrayAsync(),
                log => log.UserName == "sql-alert-test" && log.Action == "Modified" && log.Changes!.Contains("State", StringComparison.Ordinal));

            // Act & Assert: tạo phiên bản target mới phải tự kết thúc bản cũ và chặn khoảng hiệu lực chồng lấn.
            await effective.AddTargetVersionAsync(new EffectiveTargetRequest(null, null, OeePeriodTypeContract.Day,
                .80m, .88m, .92m, .995m, new DateOnly(2026, 9, 1), null, "next baseline", "DAY"), CancellationToken.None);
            db.ChangeTracker.Clear();
            var targets = await service.TargetsAsync(new TargetListQuery(PageSize: 25), CancellationToken.None);
            Assert.Equal(2, targets.TotalCount);
            Assert.Contains(targets.Items, item => item.From == new DateOnly(2026, 1, 1) && item.To == new DateOnly(2026, 8, 31));
            await Assert.ThrowsAsync<InvalidOperationException>(() => effective.AddTargetVersionAsync(new EffectiveTargetRequest(
                null, null, OeePeriodTypeContract.Day, .78m, .86m, .91m, .99m,
                new DateOnly(2026, 8, 15), null, "overlap", "DAY"), CancellationToken.None));
            db.ChangeTracker.Clear();
            var referencedTarget = (await service.TargetsAsync(new TargetListQuery(Search: "global baseline"), CancellationToken.None)).Items.Single();
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteTargetAsync(referencedTarget.Id, referencedTarget.RowVersion, CancellationToken.None));

            // Act & Assert: query realtime phải tách ConnectivityState khỏi OperationalState và dùng dữ liệu SQL thật.
            var live = await new LiveOeeQueryService(db, clock).GetAsync(new LiveOeeQuery(120), CancellationToken.None);
            Assert.Equal(2, live.Summary.Total);
            Assert.Equal(1, live.Summary.Stopped);
            Assert.Equal(1, live.Summary.Unhealthy);
            var offline = live.Machines.Single(machine => machine.Code == "M-001");
            Assert.Equal(nameof(ConnectivityStateEnum.Disconnected), offline.ConnectivityState);
            Assert.Equal(nameof(OperationalStateEnum.Stopped), offline.OperationalState);
            Assert.Equal(50m, offline.TotalQuantity);
            Assert.NotNull(live.SourceTimestamp);
            Assert.Equal("S1", live.CurrentShift);
        }
        finally
        {
            // Cleanup: chỉ xóa database test có tên đã được kiểm tra, không đụng database development.
            await db.Database.EnsureDeletedAsync();
            SqlConnection.ClearAllPools();
        }
    }

    private static async Task<SeedData> SeedAsync(OeeDbContext db, DateTimeOffset now)
    {
        // Tạo đủ cây tổ chức, master, production và PLC để truy vấn realtime chạy qua quan hệ thật.
        var enterprise = DomainTestData.Create<EnterpriseEntity>(("Code", "ENT-01"), ("Name", "Enterprise"));
        var site = DomainTestData.Create<SiteEntity>(("EnterpriseId", enterprise.Id), ("Code", "SITE-01"), ("Name", "Factory"), ("TimeZoneId", "SE Asia Standard Time"));
        var area = DomainTestData.Create<AreaEntity>(("SiteId", site.Id), ("Code", "PROC-01"), ("Name", "Area"));
        var group = DomainTestData.Create<WorkCenterEntity>(("AreaId", area.Id), ("Code", "LINE-01"), ("Name", "Line"));
        var offlineUnit = DomainTestData.Create<WorkUnitEntity>(("WorkCenterId", group.Id), ("Code", "M-001"), ("Name", "Offline"), ("OperationalState", OperationalStateEnum.Stopped));
        var stoppedUnit = DomainTestData.Create<WorkUnitEntity>(("WorkCenterId", group.Id), ("Code", "M-002"), ("Name", "Stopped"), ("OperationalState", OperationalStateEnum.Stopped));
        var shift = DomainTestData.Create<ShiftEntity>(("SiteId", site.Id), ("Code", "S1"), ("Name", "S1"), ("StartTime", new TimeOnly(6, 0)), ("EndTime", new TimeOnly(14, 0)));
        var product = DomainTestData.Create<ProductEntity>(("Code", "SKU-01"), ("Name", "Product"), ("DefaultUom", "unit"));
        var reason = DomainTestData.Create<DowntimeReasonEntity>(("Code", "DR-BREAK"), ("Name", "Breakdown"), ("Category", DowntimeCategoryEnum.AvailabilityLoss), ("Kind", DowntimeKindEnum.Breakdown));
        var connection = DomainTestData.Create<OpcUaConnectionEntity>(("Code", "OPC-01"), ("Name", "OPC"), ("EndpointUrl", "opc.tcp://test"), ("IsActive", true));
        var offlineMapping = DomainTestData.Create<PlcDeviceMappingEntity>(("OpcUaConnectionId", connection.Id), ("WorkUnitId", offlineUnit.Id), ("ExternalDeviceId", "PLC-01"), ("IsActive", true));
        var stoppedMapping = DomainTestData.Create<PlcDeviceMappingEntity>(("OpcUaConnectionId", connection.Id), ("WorkUnitId", stoppedUnit.Id), ("ExternalDeviceId", "PLC-02"), ("IsActive", true));
        var plan = DomainTestData.Create<ProductionPlanEntity>(("VersionSeriesId", Guid.CreateVersion7()), ("WorkUnitId", offlineUnit.Id), ("ProductId", product.Id), ("ShiftId", shift.Id), ("Code", "PLAN-001"), ("PlannedStart", now.AddHours(-2)), ("PlannedEnd", now.AddHours(2)), ("PlannedQuantity", 100));
        DomainTestData.Set(plan, "VersionSeriesId", plan.Id);
        var output = DomainTestData.Create<ProductionOutputEntity>(("ProductionPlanId", plan.Id), ("WorkUnitId", offlineUnit.Id), ("ProductId", product.Id), ("ShiftId", shift.Id), ("PeriodStart", now.AddHours(-1)), ("PeriodEnd", now), ("StandardSpeedPerMinute", 10), ("TotalProducedQuantity", 50), ("DefectQuantity", 2), ("Source", DataSourceEnum.Automatic));
        db.AddRange(enterprise, site, area, group, offlineUnit, stoppedUnit, shift, product, reason, connection,
            offlineMapping, stoppedMapping, plan, output);
        await db.SaveChangesAsync();
        db.AddRange(
            DomainTestData.Create<PlcDeviceRuntimeStateEntity>(("PlcDeviceMappingId", offlineMapping.Id), ("ConnectivityState", ConnectivityStateEnum.Disconnected), ("OperationalState", OperationalStateEnum.Stopped), ("LastDataAt", now.AddMinutes(-5)), ("LastTransitionAt", now.AddMinutes(-10))),
            DomainTestData.Create<PlcDeviceRuntimeStateEntity>(("PlcDeviceMappingId", stoppedMapping.Id), ("ConnectivityState", ConnectivityStateEnum.Connected), ("OperationalState", OperationalStateEnum.Stopped), ("LastDataAt", now.AddSeconds(-1)), ("LastTransitionAt", now.AddMinutes(-3))),
            DomainTestData.Create<CounterCheckpointEntity>(("PlcDeviceMappingId", offlineMapping.Id), ("LastTotalCount", 1_050), ("LastReadAt", now.AddMinutes(-5))),
            DomainTestData.Create<CounterCheckpointEntity>(("PlcDeviceMappingId", stoppedMapping.Id), ("LastTotalCount", 500), ("LastReadAt", now.AddSeconds(-1))),
            DomainTestData.Create<DowntimeEventEntity>(("WorkUnitId", stoppedUnit.Id), ("DowntimeReasonId", reason.Id), ("Category", reason.Category), ("Kind", reason.Kind), ("StartAt", now.AddMinutes(-3)), ("EndAt", null), ("Source", DataSourceEnum.Automatic)),
            CreateSnapshot(stoppedUnit.Id, now));
        await db.SaveChangesAsync();
        return new SeedData(offlineUnit, stoppedUnit);
    }

    private static OeeSnapshotEntity CreateSnapshot(Guid workUnitId, DateTimeOffset now) => DomainTestData.Create<OeeSnapshotEntity>(("WorkUnitId", workUnitId), ("PeriodType", OeePeriodTypeEnum.Day), ("PeriodCode", "DAY-20260802"), ("PeriodStart", new DateTimeOffset(2026, 8, 1, 17, 0, 0, TimeSpan.Zero)), ("PeriodEnd", new DateTimeOffset(2026, 8, 2, 17, 0, 0, TimeSpan.Zero)), ("Availability", .80m), ("Performance", .90m), ("Quality", .98m), ("Oee", .7056m), ("TotalMinutes", 1440), ("LoadingMinutes", 480), ("UtilizationMinutes", 384), ("TotalQuantity", 100), ("DefectQuantity", 2), ("GoodQuantity", 98), ("CalculationVersion", "OEE-v1"), ("CalculatedAt", now), ("InputHash", new string('A', 64)));

    private sealed record SeedData(WorkUnitEntity OfflineUnit, WorkUnitEntity StoppedUnit);
    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow { get; } = new(2026, 8, 2, 1, 0, 0, TimeSpan.Zero); }
    private sealed class TestActor : ICurrentActor { public string? UserId => Guid.Empty.ToString(); public string UserName => "sql-alert-test"; public string? IpAddress => "127.0.0.1"; }
}
