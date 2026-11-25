using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Oee.Application.Contracts.Audit;
using Oee.Domain.Entities.Organization;
using Oee.Domain.Entities.Production;
using Oee.Domain.Platform;
using Oee.Infrastructure.Audit;
using Oee.Infrastructure.Operations;
using Oee.Infrastructure.Persistence;
using Oee.Infrastructure.Persistence.Migrations;
using Oee.Tests.Fixtures;

namespace Oee.Infrastructure.IntegrationTests;

/// <summary>Kiểm chứng đổi hierarchy bảo toàn dữ liệu, concurrency token và lịch sử audit.</summary>
public sealed class EquipmentHierarchyMigrationTests
{
    [Fact]
    public void MigrationNeverDropsOrRecreatesBusinessData()
    {
        var migration = new RenameEquipmentHierarchyToAreaWorkCenter();
        foreach (var operation in migration.UpOperations.Concat(migration.DownOperations))
        {
            Assert.True(operation is RenameTableOperation or RenameColumnOperation or RenameIndexOperation or SqlOperation);
            if (operation is RenameTableOperation table)
            {
                Assert.Equal("org", table.Schema);
                Assert.Equal("org", table.NewSchema);
            }
            if (operation is SqlOperation sql)
            {
                Assert.DoesNotContain("DROP", sql.Sql, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("DELETE", sql.Sql, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("UPDATE", sql.Sql, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task UpgradeAndRollbackPreserveHierarchyVersionsProductionHistoryAndLegacyAudit()
    {
        var baseConnection = Environment.GetEnvironmentVariable("OEE_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(baseConnection)) return;
        var databaseName = $"OeeDb_HierarchyTests_{Guid.NewGuid():N}";
        var connection = new SqlConnectionStringBuilder(baseConnection) { InitialCatalog = databaseName };
        Assert.StartsWith("OeeDb_HierarchyTests_", connection.InitialCatalog, StringComparison.Ordinal);
        var options = new DbContextOptionsBuilder<OeeDbContext>().UseSqlServer(connection.ConnectionString).Options;
        await using var db = new OeeDbContext(options);
        const string previousMigration = "20260904183258_OrganizeOeeSchemas";
        var enterpriseId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var areaId = Guid.NewGuid();
        var centerId = Guid.NewGuid();
        var unitId = Guid.NewGuid();

        try
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync(previousMigration);
            // Seed đúng schema cũ để test việc nâng cấp có dữ liệu, không chỉ tạo database trống.
            await db.Database.ExecuteSqlRawAsync("""
                INSERT org.Enterprises (Id, Code, Name, Status, CreatedAt, IsDeleted)
                VALUES ({0}, N'ENT-UPGRADE', N'Enterprise', {5}, SYSDATETIMEOFFSET(), 0);
                INSERT org.Sites (Id, EnterpriseId, Code, Name, TimeZoneId, Status, CreatedAt, IsDeleted)
                VALUES ({1}, {0}, N'SITE-UPGRADE', N'Site', N'UTC', {5}, SYSDATETIMEOFFSET(), 0);
                INSERT org.Processes (Id, SiteId, Code, Name, Status, CreatedAt, IsDeleted)
                VALUES ({2}, {1}, N'AREA-UPGRADE', N'Area', {5}, SYSDATETIMEOFFSET(), 0);
                INSERT org.WorkUnitGroups (Id, ProcessId, Code, Name, Status, CreatedAt, IsDeleted)
                VALUES ({3}, {2}, N'CENTER-UPGRADE', N'Center', {5}, SYSDATETIMEOFFSET(), 0);
                INSERT org.WorkUnits (Id, WorkUnitGroupId, Code, Name, OperationalState, Status, CreatedAt, IsDeleted)
                VALUES ({4}, {3}, N'UNIT-UPGRADE', N'Unit', 0, {5}, SYSDATETIMEOFFSET(), 0);
                """, enterpriseId, siteId, areaId, centerId, unitId, 1);

            var areaVersion = await ReadVersionAsync(db, "org.Processes", areaId);
            var centerVersion = await ReadVersionAsync(db, "org.WorkUnitGroups", centerId);
            var unitVersion = await ReadVersionAsync(db, "org.WorkUnits", unitId);
            var product = DomainTestData.Create<ProductEntity>(("Code", "SKU-UPGRADE"), ("Name", "Product"), ("DefaultUom", "PCS"));
            var plan = DomainTestData.Create<ProductionPlanEntity>(("WorkUnitId", unitId), ("ProductId", product.Id), ("Code", "PLAN-UPGRADE"), ("VersionSeriesId", Guid.NewGuid()), ("PlannedStart", DateTimeOffset.UtcNow), ("PlannedEnd", DateTimeOffset.UtcNow.AddHours(1)), ("PlannedQuantity", 10));
            var areaAudit = DomainTestData.Create<AuditLogEntity>(("Timestamp", DateTimeOffset.UtcNow), ("Action", "Added"), ("EntityType", "Process"), ("EntityId", areaId.ToString()), ("Changes", "{}"));
            var centerAudit = DomainTestData.Create<AuditLogEntity>(("Timestamp", DateTimeOffset.UtcNow), ("Action", "Added"), ("EntityType", "WorkUnitGroup"), ("EntityId", centerId.ToString()), ("Changes", "{}"));
            db.Products.Add(product);
            db.ProductionPlans.Add(plan);
            db.AuditLogs.AddRange(areaAudit, centerAudit);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            await db.Database.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges());
            var area = await db.Areas.AsNoTracking().SingleAsync();
            var center = await db.WorkCenters.AsNoTracking().SingleAsync();
            var unit = await db.WorkUnits.AsNoTracking().SingleAsync();
            Assert.Equal((areaId, siteId), (area.Id, area.SiteId));
            Assert.Equal((centerId, areaId), (center.Id, center.AreaId));
            Assert.Equal((unitId, centerId), (unit.Id, unit.WorkCenterId));
            Assert.Equal(areaVersion, area.RowVersion);
            Assert.Equal(centerVersion, center.RowVersion);
            Assert.Equal(unitVersion, unit.RowVersion);
            Assert.Equal(unitId, (await db.ProductionPlans.AsNoTracking().SingleAsync()).WorkUnitId);

            var query = new MasterDataQueryService(options, new TestClock());
            Assert.Equal(siteId, (await query.AreaAsync(areaId, default)).SiteId);
            Assert.Equal(areaId, (await query.WorkCenterAsync(centerId, default)).AreaId);
            Assert.Equal("AREA-UPGRADE", (await query.WorkUnitAsync(unitId, default)).Area);
            var auditQuery = new AuditQueryService(db);
            Assert.Single((await auditQuery.SearchAsync(new AuditLogSearchRequest(EntityType: "Area"), default)).Items);
            Assert.Single((await auditQuery.SearchAsync(new AuditLogSearchRequest(EntityType: "WorkCenter"), default)).Items);
            Assert.Equal("Process", (await db.AuditLogs.AsNoTracking().SingleAsync(x => x.Id == areaAudit.Id)).EntityType);
            Assert.Equal("WorkUnitGroup", (await db.AuditLogs.AsNoTracking().SingleAsync(x => x.Id == centerAudit.Id)).EntityType);

            await migrator.MigrateAsync(previousMigration);
            Assert.Equal(areaVersion, await ReadVersionAsync(db, "org.Processes", areaId));
            Assert.Equal(centerVersion, await ReadVersionAsync(db, "org.WorkUnitGroups", centerId));
            Assert.Equal(unitVersion, await ReadVersionAsync(db, "org.WorkUnits", unitId));
            await db.Database.MigrateAsync();
            Assert.Equal(plan.Id, (await db.ProductionPlans.AsNoTracking().SingleAsync()).Id);
        }
        finally
        {
            // Chỉ dọn database tạm do test này tạo, không nhắm OeeDb hiện có.
            await db.Database.EnsureDeletedAsync();
        }
    }

    private static Task<byte[]> ReadVersionAsync(OeeDbContext db, string table, Guid id)
    {
        var sql = table switch
        {
            "org.Processes" => "SELECT RowVersion AS [Value] FROM org.Processes WHERE Id = {0}",
            "org.WorkUnitGroups" => "SELECT RowVersion AS [Value] FROM org.WorkUnitGroups WHERE Id = {0}",
            "org.WorkUnits" => "SELECT RowVersion AS [Value] FROM org.WorkUnits WHERE Id = {0}",
            _ => throw new ArgumentException("Unsupported test table.", nameof(table)),
        };
        return db.Database.SqlQueryRaw<byte[]>(sql, id).SingleAsync();
    }

    private sealed class TestClock : Oee.Application.Common.IClock
    {
        public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    }
}
