using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Oee.Application.Common;
using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.Operations;
using Oee.Application.Security;
using Oee.Domain.Common;
using Oee.Domain.Downtime;
using Oee.Domain.Entities.Production;
using Oee.Domain.PlcIntegration;
using Oee.Infrastructure.Audit;
using Oee.Infrastructure.Operations;
using Oee.Infrastructure.Persistence;
using Oee.Tests.Fixtures;
using Oee.Application.Organizations.Areas.Update;
using Oee.Application.Organizations.Sites.Update;

namespace Oee.Infrastructure.IntegrationTests;

/// <summary>
/// Integration Test trên SQL Server cho các dialog cập nhật, activate/deactivate và kết thúc hiệu lực master data.
/// </summary>
/// <remarks>
/// Test bảo vệ mã/quan hệ cha bất biến, optimistic concurrency, lịch sử nghiệp vụ và audit before/after.
/// </remarks>
public sealed class MasterDataEditActionsSqlServerTests
{
    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task EditStatusAndStandardActionsKeepParentsImmutableAndPreserveConcurrentAuditHistory()
    {
        // Arrange: tạo database test tách biệt; test được bỏ qua nếu chưa cấu hình OEE_TEST_SQLSERVER.
        var baseConnectionString = Environment.GetEnvironmentVariable("OEE_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(baseConnectionString)) return;

        var databaseName = $"OeeDb_MasterEditTests_{Guid.NewGuid():N}";
        var connection = new SqlConnectionStringBuilder(baseConnectionString) { InitialCatalog = databaseName };
        Assert.StartsWith("OeeDb_MasterEditTests_", connection.InitialCatalog, StringComparison.Ordinal);
        var actor = new TestActor();
        var options = new DbContextOptionsBuilder<OeeDbContext>()
            .UseSqlServer(connection.ConnectionString)
            .AddInterceptors(new AuditSaveChangesInterceptor(actor))
            .Options;
        await using var db = new OeeDbContext(options);
        try
        {
            // Arrange: migrate và tạo đầy đủ cây tổ chức/master thông qua Application command thật.
            await db.Database.MigrateAsync();
            var clock = new FixedClock();
            var commands = ApplicationServiceFactory.MasterData(db, clock, actor);
            var query = new MasterDataQueryService(options, clock);

            // Status chỉ nhận 0/1; số khác không được ghi xuống SQL qua command.
            await Assert.ThrowsAsync<ArgumentException>(() => commands.CreateEnterpriseAsync(
                new("ENT-BAD-STATUS", "Invalid", null, "99"), default));

            var enterprise = await commands.CreateEnterpriseAsync(new("ENT-EDIT", "Enterprise", "before", "Active"), default);
            var site = await commands.CreateSiteAsync(new(enterprise.Id, "SITE-EDIT", "Factory", "UTC", "Active"), default);
            var area = await commands.CreateAreaAsync(new(site.Id, "PROC-EDIT", "Area", "Active"), default);
            var group = await commands.CreateWorkCenterAsync(new(area.Id, "LINE-EDIT", "Line", "Active"), default);
            var unit = await commands.CreateWorkUnitAsync(new(group.Id, "M-EDIT", "Machine", "Active"), default);
            var product = await commands.CreateProductAsync(new("SKU-EDIT", "Product", "PCS", "before", "Active"), default);
            var shift = await commands.CreateShiftAsync(new(site.Id, "S3-EDIT", "Night", new TimeOnly(22, 0), new TimeOnly(6, 0), "Active"), default);
            var reason = await commands.CreateDowntimeReasonAsync(new("DR-EDIT", "Breakdown", "AvailabilityLoss", "Breakdown", "before", "Active"), default);

            // Act & Assert: server-side capability vẫn chặn update dù UI có thể đã ẩn nút.
            var deniedCommands = ApplicationServiceFactory.MasterData(db, clock, actor, new DeniedCapabilities());
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => deniedCommands.UpdateProductAsync(
                new(product.Id, "Forbidden", "PCS", null, product.RowVersion), default));

            // Act & Assert: Code phải hợp lệ, quan hệ cha vẫn bất biến.
            await Assert.ThrowsAsync<ArgumentException>(() => commands.UpdateEnterpriseAsync(
                new(enterprise.Id, "Invalid", null, enterprise.RowVersion, Code: " "), default));
            // DTO sửa chỉ chứa dữ liệu được phép thay đổi, không nhận khóa parent.
            Assert.DoesNotContain(typeof(UpdateSiteCommand).GetProperties(), property => property.Name == "EnterpriseId");
            Assert.DoesNotContain(typeof(UpdateAreaCommand).GetProperties(), property => property.Name == "SiteId");
            Assert.Equal("ENT-EDIT", (await query.EnterpriseAsync(enterprise.Id, default)).Code);

            // Act & Assert: update hợp lệ đổi RowVersion; client giữ token cũ nhận concurrency conflict.
            var enterpriseUpdated = await commands.UpdateEnterpriseAsync(
                new(enterprise.Id, "Enterprise updated", "after", enterprise.RowVersion), default);
            await Assert.ThrowsAsync<ConcurrencyConflictException>(() => commands.UpdateEnterpriseAsync(
                new(enterprise.Id, "Stale", null, enterprise.RowVersion), default));
            db.ChangeTracker.Clear();
            Assert.Equal("Enterprise updated", (await query.EnterpriseAsync(enterprise.Id, default)).Name);

            // Act & Assert: timezone được validate và group không thể tạo trùng hoặc nằm dưới parent Inactive.
            await Assert.ThrowsAsync<ArgumentException>(() => commands.UpdateSiteAsync(
                new(site.Id, "Bad zone", "Invalid/Zone", site.RowVersion), default));
            var siteUpdated = await commands.UpdateSiteAsync(new(site.Id, "Factory updated", "UTC", site.RowVersion), default);
            var areaUpdated = await commands.UpdateAreaAsync(new(area.Id, "Area updated", area.RowVersion), default);

            await Assert.ThrowsAsync<InvalidOperationException>(() => commands.CreateWorkCenterAsync(
                new(area.Id, "LINE-EDIT", "Duplicate", "Active"), default));
            var inactiveArea = await commands.CreateAreaAsync(new(site.Id, "PROC-OFF", "Inactive area", "Inactive"), default);
            await Assert.ThrowsAsync<KeyNotFoundException>(() => commands.CreateWorkCenterAsync(
                new(inactiveArea.Id, "LINE-NO", "Rejected", "Active"), default));

            // Arrange & Assert: cập nhật tên WorkUnit không được tạo mới hay làm mất PLC mapping hiện hữu.
            var opcConnection = DomainTestData.Create<OpcUaConnectionEntity>(("Code", "OPC-EDIT"), ("Name", "OPC"), ("EndpointUrl", "opc.tcp://fake:4840"), ("SecurityMode", "None"), ("SecurityPolicy", "None"), ("AuthenticationType", "Anonymous"), ("IsActive", true));
            var mapping = DomainTestData.Create<PlcDeviceMappingEntity>(("OpcUaConnectionId", opcConnection.Id), ("WorkUnitId", unit.Id), ("ExternalDeviceId", "DEV-EDIT"), ("CounterMode", CounterModeEnum.Cumulative), ("FaultCodeFormat", FaultCodeFormatEnum.Auto), ("IsActive", true));
            db.AddRange(opcConnection, mapping);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var unitUpdated = await commands.UpdateWorkUnitAsync(new(unit.Id, "Machine updated", unit.RowVersion), default);
            Assert.True(await db.PlcDeviceMappings.AsNoTracking().AnyAsync(x => x.Id == mapping.Id && x.WorkUnitId == unit.Id));

            // Arrange & Assert: đổi taxonomy lý do dừng chỉ ảnh hưởng lựa chọn tương lai, không sửa event lịch sử.
            var downtime = DomainTestData.Create<DowntimeEventEntity>(("WorkUnitId", unit.Id), ("DowntimeReasonId", reason.Id), ("Category", DowntimeCategoryEnum.AvailabilityLoss), ("Kind", DowntimeKindEnum.Breakdown), ("StartAt", clock.UtcNow.AddHours(-2)), ("EndAt", clock.UtcNow.AddHours(-1)), ("Source", DataSourceEnum.Manual));
            db.DowntimeEvents.Add(downtime);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            await Assert.ThrowsAsync<ArgumentException>(() => commands.UpdateDowntimeReasonAsync(
                new(reason.Id, "Invalid taxonomy", "QualityLoss", "Breakdown", null, reason.RowVersion), default));
            var reasonUpdated = await commands.UpdateDowntimeReasonAsync(
                new(reason.Id, "Breakdown updated", "AvailabilityLoss", "Failure", "after", reason.RowVersion), default);
            var oldEvent = await db.DowntimeEvents.AsNoTracking().SingleAsync(x => x.Id == downtime.Id);
            Assert.Equal(DowntimeKindEnum.Breakdown, oldEvent.Kind);

            // Act & Assert: ca 22:00–06:00 tiếp tục được nhận diện là ca qua nửa đêm sau update.
            var shiftUpdated = await commands.UpdateShiftAsync(
                new(shift.Id, "Night updated", new TimeOnly(22, 0), new TimeOnly(6, 0), shift.RowVersion), default);
            Assert.True((await query.ShiftAsync(shift.Id, default)).CrossesMidnight);

            // Act & Assert: deactivate là thay đổi trạng thái idempotent, không hard delete.
            var temporaryEnterprise = await commands.CreateEnterpriseAsync(new("ENT-TEMP", "Temporary", null, "Active"), default);
            var deactivated = await commands.SetEnterpriseStatusAsync(new(temporaryEnterprise.Id, temporaryEnterprise.RowVersion, "Inactive"), default);
            Assert.True(await db.Enterprises.IgnoreQueryFilters().AnyAsync(x => x.Id == temporaryEnterprise.Id && !x.IsDeleted && x.Status == RecordStatusEnum.Inactive));
            Assert.Equal("Inactive", (await query.EnterpriseAsync(temporaryEnterprise.Id, default)).Status);
            var idempotent = await commands.SetEnterpriseStatusAsync(new(temporaryEnterprise.Id, temporaryEnterprise.RowVersion, "Inactive"), default);
            Assert.Equal(deactivated.Id, idempotent.Id);
            await commands.SetEnterpriseStatusAsync(new(temporaryEnterprise.Id, deactivated.RowVersion, "Active"), default);
            Assert.Equal("Active", (await query.EnterpriseAsync(temporaryEnterprise.Id, default)).Status);

            // Act & Assert: không cho kích hoạt child khi parent đang Inactive và không tự cascade trạng thái.
            var parentForActivation = await commands.CreateEnterpriseAsync(new("ENT-PARENT", "Parent", null, "Active"), default);
            var childForActivation = await commands.CreateSiteAsync(new(parentForActivation.Id, "SITE-CHILD", "Child", "UTC", "Active"), default);
            var childOff = await commands.SetSiteStatusAsync(new(childForActivation.Id, childForActivation.RowVersion, "Inactive"), default);
            var parentOff = await commands.SetEnterpriseStatusAsync(new(parentForActivation.Id, parentForActivation.RowVersion, "Inactive"), default);
            await Assert.ThrowsAsync<InvalidOperationException>(() => commands.SetSiteStatusAsync(
                new(childForActivation.Id, childOff.RowVersion, "Active"), default));

            // Act & Assert: EffectiveTo phải hợp lệ và bản ghi đã kết thúc không còn trong filter đang hiệu lực.
            var standard = await commands.CreateStandardAsync(new(unit.Id, product.Id, 10m, "PCS",
                new DateOnly(2026, 7, 1), null, "planner"), default);
            await Assert.ThrowsAsync<ArgumentException>(() => commands.EndStandardEffectivenessAsync(
                new(standard.Id, new DateOnly(2026, 6, 30), standard.RowVersion), default));
            var ended = await commands.EndStandardEffectivenessAsync(
                new(standard.Id, new DateOnly(2026, 8, 1), standard.RowVersion), default);
            Assert.Equal(new DateOnly(2026, 8, 1), (await query.StandardAsync(standard.Id, default)).EffectiveTo);
            Assert.DoesNotContain((await query.StandardsAsync(new MasterListQuery(EffectiveOnly: true, PageSize: 50), default)).Items,
                x => x.Id == standard.Id);

            // Arrange & Assert: định mức đã được kế hoạch tham chiếu không thể xóa lịch sử.
            var referencedStandard = await commands.CreateStandardAsync(new(unit.Id, product.Id, 12m, "PCS",
                new DateOnly(2026, 8, 2), null, "planner"), default);
            var plan = DomainTestData.Create<ProductionPlanEntity>(("VersionSeriesId", Guid.CreateVersion7()), ("WorkUnitId", unit.Id), ("ProductId", product.Id), ("ShiftId", shift.Id), ("Code", "PLAN-EDIT"), ("PlannedStart", clock.UtcNow), ("PlannedEnd", clock.UtcNow.AddHours(8)), ("PlannedQuantity", 100));
            DomainTestData.Set(plan, "VersionSeriesId", plan.Id);
            db.ProductionPlans.Add(plan);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            await Assert.ThrowsAsync<InvalidOperationException>(() => commands.DeleteStandardAsync(
                referencedStandard.Id, referencedStandard.RowVersion, default));

            // Act & Assert: soft delete thiết bị chưa dùng phải lưu snapshot nhận dạng trong audit.
            var disposableUnit = await commands.CreateWorkUnitAsync(
                new(group.Id, "M-AUDIT-DELETE", "Disposable machine", "Active"), default);
            await commands.DeleteWorkUnitAsync(disposableUnit.Id, disposableUnit.RowVersion, default);
            var deleteAudits = await db.AuditLogs.AsNoTracking()
                .Where(x => x.EntityType == "WorkUnit" && x.EntityId == disposableUnit.Id.ToString()
                    && x.Action == "Deleted")
                .ToArrayAsync();
            var deleteAudit = Assert.Single(deleteAudits);
            Assert.Contains("M-AUDIT-DELETE", deleteAudit.Changes, StringComparison.Ordinal);
            Assert.Contains("Disposable machine", deleteAudit.Changes, StringComparison.Ordinal);
            Assert.DoesNotContain("Password", deleteAudit.Changes, StringComparison.OrdinalIgnoreCase);
            Assert.True(await db.WorkUnits.IgnoreQueryFilters().AsNoTracking()
                .AnyAsync(x => x.Id == disposableUnit.Id && x.IsDeleted));

            // Assert tổng hợp: audit có before/after, actor đúng và mọi RowVersion đã thay đổi.
            var audits = await db.AuditLogs.AsNoTracking().Where(x => x.UserName == actor.UserName).ToArrayAsync();
            Assert.Contains(audits, x => x.EntityType == "Enterprise" && x.Action == "Modified"
                && x.Changes!.Contains("Enterprise updated", StringComparison.Ordinal));
            Assert.Contains(audits, x => x.Action == "Deactivate" && x.Changes!.Contains("Before", StringComparison.Ordinal)
                && x.Changes.Contains("After", StringComparison.Ordinal));
            Assert.Contains(audits, x => x.Action == "Activate");
            Assert.Contains(audits, x => x.Action == "EndEffectiveness");
            Assert.Equal("master-edit-test", audits.First(x => x.Action == "EndEffectiveness").UserName);

            Assert.Equal(site.Id, (await query.AreaAsync(area.Id, default)).SiteId);
            Assert.Equal(group.Id, (await query.WorkUnitAsync(unit.Id, default)).WorkCenterId);
            Assert.Equal("ENT-EDIT", (await query.EnterpriseAsync(enterpriseUpdated.Id, default)).Code);
            Assert.NotEqual(site.RowVersion, siteUpdated.RowVersion);
            Assert.NotEqual(area.RowVersion, areaUpdated.RowVersion);
            Assert.NotEqual(unit.RowVersion, unitUpdated.RowVersion);
            Assert.NotEqual(reason.RowVersion, reasonUpdated.RowVersion);
            Assert.NotEqual(shift.RowVersion, shiftUpdated.RowVersion);
            Assert.Equal(RecordStatusEnum.Inactive, await db.Enterprises.Where(x => x.Id == parentOff.Id).Select(x => x.Status).SingleAsync());
        }
        finally
        {
            // Cleanup an toàn database test đã xác nhận prefix.
            await db.Database.EnsureDeletedAsync();
            SqlConnection.ClearAllPools();
        }
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task HierarchyDescriptionsRoundTripThroughCreateEditListsAndTree()
    {
        var source = Environment.GetEnvironmentVariable("OEE_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(source)) return;
        var connection = new SqlConnectionStringBuilder(source) { InitialCatalog = $"OeeDb_DescriptionTests_{Guid.NewGuid():N}" };
        Assert.StartsWith("OeeDb_DescriptionTests_", connection.InitialCatalog, StringComparison.Ordinal);
        var options = new DbContextOptionsBuilder<OeeDbContext>().UseSqlServer(connection.ConnectionString).Options;
        await using var db = new OeeDbContext(options);
        try
        {
            await db.Database.MigrateAsync();
            var commands = ApplicationServiceFactory.MasterData(db, new FixedClock(), new TestActor());
            var query = new MasterDataQueryService(options, new FixedClock());
            var tree = new EquipmentHierarchyQueryService(options);
            const string description = "Mô tả tiếng Việt\nDòng thứ hai";
            var padded = "  " + description + "  ";
            var root = await commands.CreateEnterpriseAsync(new("ENT-DESC", "Enterprise", padded, "Active"), default);
            var site = await commands.CreateSiteAsync(new(root.Id, "SITE-DESC", "Site", "UTC", "Active", padded), default);
            var area = await commands.CreateAreaAsync(new(site.Id, "AREA-DESC", "Area", "Active", padded), default);
            var center = await commands.CreateWorkCenterAsync(new(area.Id, "CENTER-DESC", "Center", "Active", padded), default);
            var unit = await commands.CreateWorkUnitAsync(new(center.Id, "UNIT-DESC", "Unit", "Active", padded), default);
            Assert.Equal(description, (await query.EnterpriseAsync(root.Id, default)).Description);
            Assert.Equal(description, (await query.SiteAsync(site.Id, default)).Description);
            Assert.Equal(description, (await query.AreaAsync(area.Id, default)).Description);
            Assert.Equal(description, (await query.WorkCenterAsync(center.Id, default)).Description);
            Assert.Equal(description, (await query.WorkUnitAsync(unit.Id, default)).Description);
            var path = new EquipmentHierarchyQuery[]
            {
                new(), new(EquipmentHierarchyKind.Enterprise, root.Id), new(EquipmentHierarchyKind.Site, site.Id),
                new(EquipmentHierarchyKind.Area, area.Id), new(EquipmentHierarchyKind.WorkCenter, center.Id),
            };
            foreach (var branch in path)
                Assert.Equal(description, Assert.Single((await tree.GetAsync(branch, default)).Items).Description);

            await Assert.ThrowsAsync<ArgumentException>(() => commands.CreateSiteAsync(
                new(root.Id, "BAD-DESC", "Bad", "UTC", "Active", new string('x', 501)), default));
            const string changed = "Mô tả đã cập nhật";
            var rootUpdated = await commands.UpdateEnterpriseAsync(new(root.Id, "Enterprise", changed, root.RowVersion), default);
            var siteUpdated = await commands.UpdateSiteAsync(new(site.Id, "Site", "UTC", site.RowVersion, changed), default);
            var areaUpdated = await commands.UpdateAreaAsync(new(area.Id, "Area", area.RowVersion, changed), default);
            var centerUpdated = await commands.UpdateWorkCenterAsync(new(center.Id, "Center", center.RowVersion, changed), default);
            var unitUpdated = await commands.UpdateWorkUnitAsync(new(unit.Id, "Unit", unit.RowVersion, changed), default);
            foreach (var branch in path)
                Assert.Equal(changed, Assert.Single((await tree.GetAsync(branch, default)).Items).Description);
            await Assert.ThrowsAsync<ConcurrencyConflictException>(() => commands.UpdateWorkUnitAsync(
                new(unit.Id, "Unit", unit.RowVersion, "Stale"), default));
            db.ChangeTracker.Clear();
            Assert.Equal(changed, (await query.WorkUnitAsync(unit.Id, default)).Description);

            // Mô tả rỗng được chuẩn hóa về null qua đúng luồng chỉnh sửa đang dùng.
            await commands.UpdateEnterpriseAsync(new(root.Id, "Enterprise", "  ", rootUpdated.RowVersion), default);
            await commands.UpdateSiteAsync(new(site.Id, "Site", "UTC", siteUpdated.RowVersion, "  "), default);
            await commands.UpdateAreaAsync(new(area.Id, "Area", areaUpdated.RowVersion, "  "), default);
            await commands.UpdateWorkCenterAsync(new(center.Id, "Center", centerUpdated.RowVersion, "  "), default);
            await commands.UpdateWorkUnitAsync(new(unit.Id, "Unit", unitUpdated.RowVersion, "  "), default);
            foreach (var branch in path)
                Assert.Null(Assert.Single((await tree.GetAsync(branch, default)).Items).Description);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
            SqlConnection.ClearAllPools();
        }
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task HierarchyCodeEditsAreUniqueConcurrentAuditedAndKeepForeignKeys()
    {
        var source = Environment.GetEnvironmentVariable("OEE_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(source)) return;
        var connection = new SqlConnectionStringBuilder(source) { InitialCatalog = $"OeeDb_CodeEditTests_{Guid.NewGuid():N}" };
        Assert.StartsWith("OeeDb_CodeEditTests_", connection.InitialCatalog, StringComparison.Ordinal);
        var actor = new TestActor();
        var options = new DbContextOptionsBuilder<OeeDbContext>().UseSqlServer(connection.ConnectionString)
            .AddInterceptors(new AuditSaveChangesInterceptor(actor)).Options;
        await using var db = new OeeDbContext(options);
        try
        {
            await db.Database.MigrateAsync();
            var clock = new FixedClock();
            var commands = ApplicationServiceFactory.MasterData(db, clock, actor);
            var query = new MasterDataQueryService(options, clock);
            var root = await commands.CreateEnterpriseAsync(new("ENT-OLD", "Enterprise", null, "Active"), default);
            var site = await commands.CreateSiteAsync(new(root.Id, "SITE-OLD", "Site", "UTC", "Active"), default);
            var area = await commands.CreateAreaAsync(new(site.Id, "AREA-OLD", "Area", "Active"), default);
            var center = await commands.CreateWorkCenterAsync(new(area.Id, "CENTER-OLD", "Center", "Active"), default);
            var unit = await commands.CreateWorkUnitAsync(new(center.Id, "UNIT-OLD", "Unit", "Active"), default);
            var product = await commands.CreateProductAsync(new("SKU-CODE", "Product", "PCS", null, "Active"), default);
            var standard = await commands.CreateStandardAsync(new(unit.Id, product.Id, 10, "PCS", new DateOnly(2026, 1, 1), null, null), default);
            var shift = await commands.CreateShiftAsync(new(site.Id, "S1", "Morning", new TimeOnly(6, 0), new TimeOnly(14, 0), "Active"), default);
            await commands.CreateEnterpriseAsync(new("DUP", "Other", null, "Active"), default);
            await commands.CreateSiteAsync(new(root.Id, "DUP", "Other", "UTC", "Active"), default);
            await commands.CreateAreaAsync(new(site.Id, "DUP", "Other", "Active"), default);
            await commands.CreateWorkCenterAsync(new(area.Id, "DUP", "Other", "Active"), default);
            await commands.CreateWorkUnitAsync(new(center.Id, "DUP", "Other", "Active"), default);

            var rootNew = await commands.UpdateEnterpriseAsync(new(root.Id, "Enterprise", null, root.RowVersion, "  ENT-NEW  "), default);
            var siteNew = await commands.UpdateSiteAsync(new(site.Id, "Site", "UTC", site.RowVersion, Code: "SITE-NEW"), default);
            var areaNew = await commands.UpdateAreaAsync(new(area.Id, "Area", area.RowVersion, Code: "AREA-NEW"), default);
            var centerNew = await commands.UpdateWorkCenterAsync(new(center.Id, "Center", center.RowVersion, Code: "CENTER-NEW"), default);
            var unitNew = await commands.UpdateWorkUnitAsync(new(unit.Id, "Unit", unit.RowVersion, Code: "UNIT-NEW"), default);
            Assert.Equal("ENT-NEW", (await query.EnterpriseAsync(root.Id, default)).Code);
            Assert.Equal("SITE-NEW", (await query.SiteAsync(site.Id, default)).Code);
            Assert.Equal("AREA-NEW", (await query.AreaAsync(area.Id, default)).Code);
            Assert.Equal("CENTER-NEW", (await query.WorkCenterAsync(center.Id, default)).Code);
            Assert.Equal("UNIT-NEW", (await query.WorkUnitAsync(unit.Id, default)).Code);
            Assert.Equal(root.Id, (await query.SiteAsync(site.Id, default)).EnterpriseId);
            Assert.Equal(site.Id, (await query.AreaAsync(area.Id, default)).SiteId);
            Assert.Equal(area.Id, (await query.WorkCenterAsync(center.Id, default)).AreaId);
            Assert.Equal(center.Id, (await query.WorkUnitAsync(unit.Id, default)).WorkCenterId);
            Assert.Equal(unit.Id, (await query.StandardAsync(standard.Id, default)).WorkUnitId);
            Assert.Equal(site.Id, (await query.ShiftAsync(shift.Id, default)).SiteId);
            var audits = await db.AuditLogs.AsNoTracking().Where(x => x.Action == "Modified").ToArrayAsync();
            foreach (var prefix in new[] { "ENT", "SITE", "AREA", "CENTER", "UNIT" })
                Assert.Contains(audits, x => x.Changes!.Contains(prefix + "-OLD", StringComparison.Ordinal)
                    && x.Changes.Contains(prefix + "-NEW", StringComparison.Ordinal));

            await Assert.ThrowsAsync<InvalidOperationException>(() => commands.UpdateEnterpriseAsync(new(root.Id, "Enterprise", null, rootNew.RowVersion, "DUP"), default));
            await Assert.ThrowsAsync<InvalidOperationException>(() => commands.UpdateSiteAsync(new(site.Id, "Site", "UTC", siteNew.RowVersion, Code: "DUP"), default));
            await Assert.ThrowsAsync<InvalidOperationException>(() => commands.UpdateAreaAsync(new(area.Id, "Area", areaNew.RowVersion, Code: "DUP"), default));
            await Assert.ThrowsAsync<InvalidOperationException>(() => commands.UpdateWorkCenterAsync(new(center.Id, "Center", centerNew.RowVersion, Code: "DUP"), default));
            await Assert.ThrowsAsync<InvalidOperationException>(() => commands.UpdateWorkUnitAsync(new(unit.Id, "Unit", unitNew.RowVersion, Code: "DUP"), default));
            await Assert.ThrowsAsync<ArgumentException>(() => commands.UpdateWorkUnitAsync(new(unit.Id, "Unit", unitNew.RowVersion, Code: " "), default));
            await Assert.ThrowsAsync<ArgumentException>(() => commands.UpdateWorkUnitAsync(new(unit.Id, "Unit", unitNew.RowVersion, Code: new string('x', 33)), default));
            await Assert.ThrowsAsync<ConcurrencyConflictException>(() => commands.UpdateWorkUnitAsync(new(unit.Id, "Unit", unit.RowVersion, Code: "STALE"), default));
            db.ChangeTracker.Clear();
            Assert.Equal("UNIT-NEW", (await query.WorkUnitAsync(unit.Id, default)).Code);

            // Đổi mã lần nữa vẫn giữ parent và kiểm tra RowVersion hiện tại.
            await commands.UpdateEnterpriseAsync(new(root.Id, "Enterprise", null, rootNew.RowVersion, "ENT-FINAL"), default);
            await commands.UpdateSiteAsync(new(site.Id, "Site", "UTC", siteNew.RowVersion, Code: "SITE-FINAL"), default);
            await commands.UpdateAreaAsync(new(area.Id, "Area", areaNew.RowVersion, Code: "AREA-FINAL"), default);
            await commands.UpdateWorkCenterAsync(new(center.Id, "Center", centerNew.RowVersion, Code: "CENTER-FINAL"), default);
            await commands.UpdateWorkUnitAsync(new(unit.Id, "Unit", unitNew.RowVersion, Code: "UNIT-FINAL"), default);
            Assert.Equal("ENT-FINAL", (await query.EnterpriseAsync(root.Id, default)).Code);
            Assert.Equal("SITE-FINAL", (await query.SiteAsync(site.Id, default)).Code);
            Assert.Equal("AREA-FINAL", (await query.AreaAsync(area.Id, default)).Code);
            Assert.Equal("CENTER-FINAL", (await query.WorkCenterAsync(center.Id, default)).Code);
            Assert.Equal("UNIT-FINAL", (await query.WorkUnitAsync(unit.Id, default)).Code);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
            SqlConnection.ClearAllPools();
        }
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task BulkDeleteIsChildrenFirstAuditedAndLimitedToExplicitSelection()
    {
        var source = Environment.GetEnvironmentVariable("OEE_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(source)) return;
        var connection = new SqlConnectionStringBuilder(source) { InitialCatalog = $"OeeDb_BulkDeleteTests_{Guid.NewGuid():N}" };
        Assert.StartsWith("OeeDb_BulkDeleteTests_", connection.InitialCatalog, StringComparison.Ordinal);
        var actor = new TestActor();
        var options = new DbContextOptionsBuilder<OeeDbContext>().UseSqlServer(connection.ConnectionString)
            .AddInterceptors(new AuditSaveChangesInterceptor(actor)).Options;
        await using var db = new OeeDbContext(options);
        try
        {
            await db.Database.MigrateAsync();
            var commands = ApplicationServiceFactory.MasterData(db, new FixedClock(), actor);
            var root = await commands.CreateEnterpriseAsync(new("BULK", "Root", null, "Active"), default);
            var site = await commands.CreateSiteAsync(new(root.Id, "SITE", "Site", "UTC", "Active"), default);
            var area = await commands.CreateAreaAsync(new(site.Id, "AREA", "Area", "Active"), default);
            var center = await commands.CreateWorkCenterAsync(new(area.Id, "CENTER", "Center", "Active"), default);
            var unit = await commands.CreateWorkUnitAsync(new(center.Id, "UNIT", "Unit", "Active"), default);
            var outside = await commands.CreateEnterpriseAsync(new("OUTSIDE", "Outside", null, "Active"), default);
            EquipmentHierarchyDeleteItem[] selected =
            [
                new(EquipmentHierarchyKind.Enterprise, root.Id, root.RowVersion),
                new(EquipmentHierarchyKind.Site, site.Id, site.RowVersion),
                new(EquipmentHierarchyKind.Area, area.Id, area.RowVersion),
                new(EquipmentHierarchyKind.WorkCenter, center.Id, center.RowVersion),
                new(EquipmentHierarchyKind.WorkUnit, unit.Id, unit.RowVersion),
            ];
            var denied = ApplicationServiceFactory.MasterData(db, new FixedClock(), actor, new DeniedCapabilities());
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => denied.DeleteHierarchyAsync(selected, default));
            await Assert.ThrowsAsync<ArgumentException>(() => commands.DeleteHierarchyAsync([], default));
            await Assert.ThrowsAsync<ArgumentException>(() => commands.DeleteHierarchyAsync([selected[0], selected[0]], default));
            await commands.DeleteHierarchyAsync(selected, default);
            Assert.True(await db.Enterprises.IgnoreQueryFilters().AnyAsync(x => x.Id == root.Id && x.IsDeleted));
            Assert.True(await db.Sites.IgnoreQueryFilters().AnyAsync(x => x.Id == site.Id && x.IsDeleted));
            Assert.True(await db.Areas.IgnoreQueryFilters().AnyAsync(x => x.Id == area.Id && x.IsDeleted));
            Assert.True(await db.WorkCenters.IgnoreQueryFilters().AnyAsync(x => x.Id == center.Id && x.IsDeleted));
            Assert.True(await db.WorkUnits.IgnoreQueryFilters().AnyAsync(x => x.Id == unit.Id && x.IsDeleted));
            Assert.True(await db.Enterprises.AnyAsync(x => x.Id == outside.Id));
            Assert.Equal(5, await db.AuditLogs.CountAsync(x => x.Action == "Deleted"));
        }
        finally { await db.Database.EnsureDeletedAsync(); SqlConnection.ClearAllPools(); }
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task BulkDeleteRollsBackRowsAndAuditForHistoryUnselectedChildrenAndStaleTokens()
    {
        var source = Environment.GetEnvironmentVariable("OEE_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(source)) return;
        var connection = new SqlConnectionStringBuilder(source) { InitialCatalog = $"OeeDb_BulkRollbackTests_{Guid.NewGuid():N}" };
        Assert.StartsWith("OeeDb_BulkRollbackTests_", connection.InitialCatalog, StringComparison.Ordinal);
        var actor = new TestActor();
        var options = new DbContextOptionsBuilder<OeeDbContext>().UseSqlServer(connection.ConnectionString)
            .AddInterceptors(new AuditSaveChangesInterceptor(actor)).Options;
        await using var db = new OeeDbContext(options);
        try
        {
            await db.Database.MigrateAsync();
            var commands = ApplicationServiceFactory.MasterData(db, new FixedClock(), actor);
            var root = await commands.CreateEnterpriseAsync(new("ROLLBACK", "Root", null, "Active"), default);
            var site = await commands.CreateSiteAsync(new(root.Id, "SITE", "Site", "UTC", "Active"), default);
            var area = await commands.CreateAreaAsync(new(site.Id, "AREA", "Area", "Active"), default);
            var center = await commands.CreateWorkCenterAsync(new(area.Id, "CENTER", "Center", "Active"), default);
            var good = await commands.CreateWorkUnitAsync(new(center.Id, "GOOD", "Good", "Active"), default);
            var blocked = await commands.CreateWorkUnitAsync(new(center.Id, "BLOCKED", "Blocked", "Active"), default);
            Assert.True(good.Id.CompareTo(blocked.Id) < 0);
            var product = await commands.CreateProductAsync(new("SKU", "Product", "PCS", null, "Active"), default);
            await commands.CreateStandardAsync(new(blocked.Id, product.Id, 10, "PCS", new DateOnly(2026, 1, 1), null, null), default);
            var goodItem = new EquipmentHierarchyDeleteItem(EquipmentHierarchyKind.WorkUnit, good.Id, good.RowVersion);
            await Assert.ThrowsAsync<InvalidOperationException>(() => commands.DeleteHierarchyAsync(
                [goodItem, new(EquipmentHierarchyKind.WorkUnit, blocked.Id, blocked.RowVersion)], default));
            Assert.True(await db.WorkUnits.AnyAsync(x => x.Id == good.Id));
            Assert.Equal(Convert.FromBase64String(good.RowVersion), await db.WorkUnits.Where(x => x.Id == good.Id).Select(x => x.RowVersion).SingleAsync());
            Assert.Equal(0, await db.AuditLogs.CountAsync(x => x.Action == "Deleted"));

            await Assert.ThrowsAsync<InvalidOperationException>(() => commands.DeleteHierarchyAsync(
                [goodItem, new(EquipmentHierarchyKind.WorkCenter, center.Id, center.RowVersion)], default));
            Assert.True(await db.WorkUnits.AnyAsync(x => x.Id == good.Id));
            Assert.True(await db.WorkCenters.AnyAsync(x => x.Id == center.Id));
            Assert.Equal(0, await db.AuditLogs.CountAsync(x => x.Action == "Deleted"));

            var later = await commands.CreateWorkUnitAsync(new(center.Id, "LATER", "Later", "Active"), default);
            await commands.UpdateWorkUnitAsync(new(later.Id, "Changed", later.RowVersion), default);
            await Assert.ThrowsAsync<ConcurrencyConflictException>(() => commands.DeleteHierarchyAsync(
                [goodItem, new(EquipmentHierarchyKind.WorkUnit, later.Id, later.RowVersion)], default));
            Assert.True(await db.WorkUnits.AnyAsync(x => x.Id == good.Id));
            Assert.True(await db.WorkUnits.AnyAsync(x => x.Id == later.Id));
            Assert.Equal(0, await db.AuditLogs.CountAsync(x => x.Action == "Deleted"));
        }
        finally { await db.Database.EnsureDeletedAsync(); SqlConnection.ClearAllPools(); }
    }

    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow { get; } = new(2026, 8, 12, 12, 0, 0, TimeSpan.Zero); }
    private sealed class TestActor : ICurrentActor { public string? UserId => Guid.Empty.ToString(); public string UserName => "master-edit-test"; public string? IpAddress => "127.0.0.1"; }
    private sealed class DeniedCapabilities : ICapabilityChecker { public bool HasCapability(string capability) => false; }
}
