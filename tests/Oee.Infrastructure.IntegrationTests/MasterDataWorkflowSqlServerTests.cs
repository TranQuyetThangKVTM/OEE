using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Oee.Application.Common;
using Oee.Application.Contracts.Modules;
using Oee.Application.Security;
using Oee.Domain.Common;
using Oee.Domain.Downtime;
using Oee.Domain.Entities.Production;
using Oee.Infrastructure.Audit;
using Oee.Infrastructure.Operations;
using Oee.Infrastructure.Persistence;
using Oee.Tests.Fixtures;

namespace Oee.Infrastructure.IntegrationTests;

/// <summary>
/// Integration Test trên SQL Server cho luồng tạo, tìm kiếm, cập nhật và xóa an toàn dữ liệu nền.
/// </summary>
public sealed class MasterDataWorkflowSqlServerTests
{
    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task MasterWorkflowEnforcesHierarchyUniquenessEffectiveRangesConcurrencyAuditAndReferences()
    {
        // Arrange: mỗi lượt test dùng schema thật trong database riêng để kiểm chứng constraint quan hệ.
        var baseConnectionString = Environment.GetEnvironmentVariable("OEE_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(baseConnectionString)) return;

        var databaseName = $"OeeDb_MasterTests_{Guid.NewGuid():N}";
        var connection = new SqlConnectionStringBuilder(baseConnectionString) { InitialCatalog = databaseName };
        Assert.StartsWith("OeeDb_MasterTests_", connection.InitialCatalog, StringComparison.Ordinal);
        var options = new DbContextOptionsBuilder<OeeDbContext>()
            .UseSqlServer(connection.ConnectionString)
            .AddInterceptors(new AuditSaveChangesInterceptor(new TestActor()))
            .Options;
        await using var db = new OeeDbContext(options);
        try
        {
            // Arrange & Act: tạo cây Enterprise → Site → Area → Group → WorkUnit cùng các master liên quan.
            await db.Database.MigrateAsync();
            var clock = new FixedClock();
            var commands = ApplicationServiceFactory.MasterData(db, clock);
            var query = new MasterDataQueryService(options, clock);

            var enterprise = await commands.CreateEnterpriseAsync(new("ENT-01", "Enterprise", "root", "Active"), CancellationToken.None);
            await Assert.ThrowsAsync<InvalidOperationException>(() => commands.CreateEnterpriseAsync(
                new("ENT-01", "Duplicate", null, "Active"), CancellationToken.None));
            var site = await commands.CreateSiteAsync(new(enterprise.Id, "SITE-01", "Factory", "UTC", "Active"), CancellationToken.None);
            await Assert.ThrowsAsync<ArgumentException>(() => commands.CreateSiteAsync(
                new(enterprise.Id, "SITE-BAD", "Bad time zone", "Not/A-TimeZone", "Active"), CancellationToken.None));
            var area = await commands.CreateAreaAsync(new(site.Id, "PROC-01", "Area", "Active"), CancellationToken.None);
            var group = await commands.CreateWorkCenterAsync(new(area.Id, "LINE-01", "Line", "Active"), CancellationToken.None);
            var workUnit = await commands.CreateWorkUnitAsync(new(group.Id, "M-001", "Machine", "Active"), CancellationToken.None);
            var product = await commands.CreateProductAsync(new("SKU-001", "Product", "unit", "primary", "Active"), CancellationToken.None);
            await Assert.ThrowsAsync<InvalidOperationException>(() => commands.CreateProductAsync(
                new("  SKU-001  ", "Trimmed duplicate", "PCS", null, "Active"), CancellationToken.None));
            var shift = await commands.CreateShiftAsync(new(site.Id, "S3", "Night shift", new TimeOnly(22, 0),
                new TimeOnly(6, 0), "Active"), CancellationToken.None);
            var unknown = await commands.CreateDowntimeReasonAsync(new("DR-UNKNOWN", "Unknown", "Unknown", "Unknown", null, "Active"), CancellationToken.None);
            var reason = await commands.CreateDowntimeReasonAsync(new("DR-BREAK", "Breakdown", "AvailabilityLoss", "Breakdown", null, "Active"), CancellationToken.None);

            // Assert: không cho tạo child dưới parent Inactive.
            var inactiveEnterprise = await commands.CreateEnterpriseAsync(new("ENT-INACTIVE", "Inactive", null, "Inactive"), CancellationToken.None);
            await Assert.ThrowsAsync<KeyNotFoundException>(() => commands.CreateSiteAsync(
                new(inactiveEnterprise.Id, "SITE-NO", "Must reject", "UTC", "Active"), CancellationToken.None));

            // Act & Assert: version định mức tự khép khoảng cũ và chặn khoảng hiệu lực chồng lấn.
            var firstStandard = await commands.CreateStandardAsync(new(workUnit.Id, product.Id, 10m, "unit",
                new DateOnly(2026, 8, 1), null, "planner"), CancellationToken.None);
            await commands.CreateStandardAsync(new(workUnit.Id, product.Id, 12m, "unit",
                new DateOnly(2026, 9, 1), null, "planner"), CancellationToken.None);
            await Assert.ThrowsAsync<InvalidOperationException>(() => commands.CreateStandardAsync(new(workUnit.Id,
                product.Id, 11m, "unit", new DateOnly(2026, 8, 15), new DateOnly(2026, 9, 15), "planner"), CancellationToken.None));

            var standards = await query.StandardsAsync(new MasterListQuery(ParentId: workUnit.Id,
                RelatedId: product.Id, EffectiveOnly: true, SortBy: "speed", PageSize: 10), CancellationToken.None);
            Assert.Single(standards.Items);
            Assert.Equal(10m, standards.Items.Single().SpeedPerMinute);
            Assert.Equal(new DateOnly(2026, 8, 31), standards.Items.Single().EffectiveTo);

            // Act & Assert: RowVersion mới được trả sau update và token cũ bị SQL Server từ chối.
            var updatedProduct = await commands.UpdateProductAsync(
                new(product.Id, "Product updated", "unit", "changed", product.RowVersion), CancellationToken.None);
            // Luồng trạng thái riêng bảo vệ dependency; sửa thông tin không được bỏ qua hàng rào này.
            await Assert.ThrowsAsync<InvalidOperationException>(() => commands.SetProductStatusAsync(
                new(product.Id, updatedProduct.RowVersion, "Inactive"), CancellationToken.None));
            await Assert.ThrowsAsync<ConcurrencyConflictException>(() => commands.UpdateProductAsync(
                new(product.Id, "Stale update", "unit", null, product.RowVersion), CancellationToken.None));
            Assert.NotEqual(product.RowVersion, updatedProduct.RowVersion);
            db.ChangeTracker.Clear();

            // Act & Assert: query filter/sort/projection đọc dữ liệu thật, gồm nhận diện ca qua nửa đêm.
            var productPage = await query.ProductsAsync(new MasterListQuery(Search: "updated", Status: "Active",
                SortBy: "name", PageSize: 10), CancellationToken.None);
            Assert.Single(productPage.Items);
            var shiftPage = await query.ShiftsAsync(new MasterListQuery(ParentId: site.Id, PageSize: 10), CancellationToken.None);
            Assert.True(shiftPage.Items.Single().CrossesMidnight);
            Assert.Equal(enterprise.Id, (await query.EnterpriseAsync(enterprise.Id, CancellationToken.None)).Id);
            Assert.Equal(enterprise.Id, (await query.SiteAsync(site.Id, CancellationToken.None)).EnterpriseId);
            Assert.Equal(site.Id, (await query.AreaAsync(area.Id, CancellationToken.None)).SiteId);
            Assert.Equal(area.Id, (await query.WorkCenterAsync(group.Id, CancellationToken.None)).AreaId);
            Assert.Equal(group.Id, (await query.WorkUnitAsync(workUnit.Id, CancellationToken.None)).WorkCenterId);
            Assert.Equal(2, (await query.DowntimeReasonsAsync(new MasterListQuery(PageSize: 10), CancellationToken.None)).TotalCount);

            // Regression: Blazor có thể tải lookup và bảng cùng lúc. Mỗi query phải có DbContext
            // riêng; nếu dùng chung context, EF Core sẽ ném lỗi "A second operation was started".
            var concurrentLookups = query.LookupsAsync(CancellationToken.None);
            var concurrentProducts = query.ProductsAsync(new MasterListQuery(PageSize: 10), CancellationToken.None);
            var concurrentStandards = query.StandardsAsync(new MasterListQuery(PageSize: 10), CancellationToken.None);
            await Task.WhenAll(concurrentLookups, concurrentProducts, concurrentStandards);
            Assert.NotEmpty((await concurrentLookups).Products);
            Assert.NotEmpty((await concurrentProducts).Items);
            Assert.NotEmpty((await concurrentStandards).Items);

            // Arrange: tạo fact/history tham chiếu master để kiểm tra hàng rào xóa an toàn.
            var plan = DomainTestData.Create<ProductionPlanEntity>(("VersionSeriesId", Guid.CreateVersion7()), ("WorkUnitId", workUnit.Id), ("ProductId", product.Id), ("ShiftId", shift.Id), ("Code", "PLAN-MASTER"), ("PlannedStart", clock.UtcNow.AddHours(-1)), ("PlannedEnd", clock.UtcNow.AddHours(1)), ("PlannedQuantity", 100));
            DomainTestData.Set(plan, "VersionSeriesId", plan.Id);
            var output = DomainTestData.Create<ProductionOutputEntity>(("ProductionPlanId", plan.Id), ("WorkUnitId", workUnit.Id), ("ProductId", product.Id), ("ShiftId", shift.Id), ("PeriodStart", clock.UtcNow.AddMinutes(-30)), ("PeriodEnd", clock.UtcNow), ("StandardSpeedPerMinute", 10), ("TotalProducedQuantity", 50), ("DefectQuantity", 1), ("Source", DataSourceEnum.Manual));
            var downtime = DomainTestData.Create<DowntimeEventEntity>(("WorkUnitId", workUnit.Id), ("DowntimeReasonId", reason.Id), ("Category", DowntimeCategoryEnum.AvailabilityLoss), ("Kind", DowntimeKindEnum.Breakdown), ("StartAt", clock.UtcNow.AddHours(-2)), ("EndAt", clock.UtcNow.AddHours(-1)), ("Source", DataSourceEnum.Manual));
            db.AddRange(plan, output, downtime);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            // Act & Assert: tất cả master đã được tham chiếu đều bị chặn xóa thay vì cascade mất lịch sử.
            await Assert.ThrowsAsync<InvalidOperationException>(() => commands.DeleteEnterpriseAsync(enterprise.Id, enterprise.RowVersion, CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => commands.DeleteSiteAsync(site.Id, site.RowVersion, CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => commands.DeleteAreaAsync(area.Id, area.RowVersion, CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => commands.DeleteWorkCenterAsync(group.Id, group.RowVersion, CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => commands.DeleteWorkUnitAsync(workUnit.Id, workUnit.RowVersion, CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => commands.DeleteShiftAsync(shift.Id, shift.RowVersion, CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => commands.DeleteProductAsync(product.Id, updatedProduct.RowVersion, CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => commands.DeleteStandardAsync(firstStandard.Id, firstStandard.RowVersion, CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => commands.DeleteDowntimeReasonAsync(reason.Id, reason.RowVersion, CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => commands.DeleteDowntimeReasonAsync(unknown.Id, unknown.RowVersion, CancellationToken.None));

            // Act & Assert: master chưa dùng được soft delete và bị query filter loại khỏi danh sách thường.
            var disposable = await commands.CreateProductAsync(new("SKU-TEMP", "Temporary", "unit", null, "Active"), CancellationToken.None);
            disposable = await commands.SetProductStatusAsync(new(disposable.Id, disposable.RowVersion, "Inactive"), CancellationToken.None);
            await commands.DeleteProductAsync(disposable.Id, disposable.RowVersion, CancellationToken.None);
            Assert.False(await db.Products.AnyAsync(x => x.Id == disposable.Id));
            Assert.True(await db.Products.IgnoreQueryFilters().AnyAsync(x => x.Id == disposable.Id && x.IsDeleted));

            // Assert: interceptor ghi audit cho mọi loại master mà workflow đã thay đổi.
            var masterAudits = await db.AuditLogs.AsNoTracking().Where(x => x.EntityType == "Product" || x.EntityType == "Site"
                || x.EntityType == "WorkUnitStandard" || x.EntityType == "Enterprise" || x.EntityType == "Area"
                || x.EntityType == "WorkCenter" || x.EntityType == "WorkUnit" || x.EntityType == "Shift"
                || x.EntityType == "DowntimeReason").ToArrayAsync();
            Assert.Contains(masterAudits, x => x.Action == "Added");
            Assert.Contains(masterAudits, x => x.Action == "Modified" && x.Changes != null && x.Changes.Contains("Status", StringComparison.Ordinal));
            foreach (var entityType in new[] { "Enterprise", "Site", "Area", "WorkCenter", "WorkUnit", "Product", "Shift", "DowntimeReason", "WorkUnitStandard" })
                Assert.Contains(masterAudits, x => x.EntityType == entityType && x.Action == "Added");
        }
        finally
        {
            // Cleanup database test tách biệt.
            await db.Database.EnsureDeletedAsync();
            SqlConnection.ClearAllPools();
        }
    }

    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow { get; } = new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero); }
    private sealed class TestActor : ICurrentActor { public string? UserId => Guid.Empty.ToString(); public string UserName => "master-test"; public string? IpAddress => "127.0.0.1"; }
}
