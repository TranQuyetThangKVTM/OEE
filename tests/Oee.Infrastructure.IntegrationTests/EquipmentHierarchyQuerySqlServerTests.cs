using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Oee.Application.Contracts.Modules;
using Oee.Domain.Common;
using Oee.Domain.Entities.Organization;
using Oee.Infrastructure.Operations;
using Oee.Infrastructure.Persistence;
using Oee.Tests.Fixtures;

namespace Oee.Infrastructure.IntegrationTests;

public sealed class EquipmentHierarchyQuerySqlServerTests
{
    private static readonly string[] ExpectedSecondPage = ["UNIT04", "UNIT05", "UNIT06"];

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task LevelFiltersCombineOnOnePathPreserveAncestorsAndKeepEmptyMatchingParents()
    {
        var source = Environment.GetEnvironmentVariable("OEE_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(source)) return;
        var connection = new SqlConnectionStringBuilder(source) { InitialCatalog = $"OeeDb_TreeFilters_{Guid.NewGuid():N}" };
        Assert.StartsWith("OeeDb_TreeFilters_", connection.InitialCatalog, StringComparison.Ordinal);
        var options = new DbContextOptionsBuilder<OeeDbContext>().UseSqlServer(connection.ConnectionString).Options;
        await using var db = new OeeDbContext(options);
        try
        {
            await db.Database.MigrateAsync();
            var enterprise = DomainTestData.Create<EnterpriseEntity>(("Code", "MCH"), ("Name", "Masan"));
            var site = DomainTestData.Create<SiteEntity>(("Enterprise", enterprise), ("Code", "BEV"), ("Name", "Beverage"), ("TimeZoneId", "UTC"));
            var otherSite = DomainTestData.Create<SiteEntity>(("Enterprise", enterprise), ("Code", "FOOD"), ("Name", "Food"), ("TimeZoneId", "UTC"));
            var area = DomainTestData.Create<AreaEntity>(("Site", site), ("Code", "PACK"), ("Name", "Packing"));
            var siblingArea = DomainTestData.Create<AreaEntity>(("Site", site), ("Code", "PROCESS"), ("Name", "Processing"));
            var otherArea = DomainTestData.Create<AreaEntity>(("Site", otherSite), ("Code", "PACK"), ("Name", "Packing"));
            var center = DomainTestData.Create<WorkCenterEntity>(("Area", area), ("Code", "LINE"), ("Name", "Packing line"));
            var siblingCenter = DomainTestData.Create<WorkCenterEntity>(("Area", siblingArea), ("Code", "MIX"), ("Name", "Mixer line"));
            var otherCenter = DomainTestData.Create<WorkCenterEntity>(("Area", otherArea), ("Code", "LINE"), ("Name", "Packing line"));
            var emptyCenter = DomainTestData.Create<WorkCenterEntity>(("Area", area), ("Code", "EMPTY"), ("Name", "Empty line"));
            var filler = DomainTestData.Create<WorkUnitEntity>(("WorkCenter", center), ("Code", "F01"), ("Name", "Filler"));
            db.AddRange(filler, DomainTestData.Create<WorkUnitEntity>(("WorkCenter", center), ("Code", "L01"), ("Name", "Labeler")),
                DomainTestData.Create<WorkUnitEntity>(("WorkCenter", siblingCenter), ("Code", "M01"), ("Name", "Mixer")),
                DomainTestData.Create<WorkUnitEntity>(("WorkCenter", otherCenter), ("Code", "F01"), ("Name", "Filler")),
                DomainTestData.Create<WorkUnitEntity>(("WorkCenter", center), ("Code", "GHOST"), ("Name", "Deleted"), ("IsDeleted", true)),
                emptyCenter, DomainTestData.Create<EnterpriseEntity>(("Code", "EMPTY"), ("Name", "Empty enterprise")));
            await db.SaveChangesAsync();
            var query = new EquipmentHierarchyQueryService(options);
            var filter = new EquipmentHierarchyQuery(EnterpriseSearch: "  MCH ", SiteSearch: "Beverage",
                AreaSearch: "PACK", WorkCenterSearch: "LINE", WorkUnitSearch: "Filler");
            var expected = new[] { enterprise.Id, site.Id, area.Id, center.Id, filler.Id };
            for (var level = 0; level < 5; level++)
            {
                var request = filter with { ParentKind = level == 0 ? null : (EquipmentHierarchyKind)(level - 1),
                    ParentId = level == 0 ? null : expected[level - 1], PageSize = 1 };
                var result = await query.GetAsync(request, default);
                Assert.Equal(1, result.TotalCount);
                Assert.Equal(expected[level], result.Items.Single().Id);
            }
            Assert.Empty((await query.GetAsync(new(SiteSearch: "BEV", AreaSearch: "PROCESS", WorkUnitSearch: "Filler"), default)).Items);
            Assert.Empty((await query.GetAsync(new(WorkUnitSearch: "MCH"), default)).Items);
            Assert.Empty((await query.GetAsync(new(WorkUnitSearch: "GHOST"), default)).Items);
            Assert.Equal(enterprise.Id, (await query.GetAsync(new(WorkUnitSearch: "Filler"), default)).Items.Single().Id);
            Assert.Equal(2, (await query.GetAsync(new(EquipmentHierarchyKind.Enterprise, enterprise.Id, WorkUnitSearch: "Filler"), default)).TotalCount);
            Assert.Equal(emptyCenter.Id, (await query.GetAsync(new(EquipmentHierarchyKind.Area, area.Id, WorkCenterSearch: "EMPTY"), default)).Items.Single().Id);
            Assert.Equal(2, (await query.GetAsync(new(EquipmentHierarchyKind.WorkCenter, center.Id, SiteSearch: "BEV"), default)).TotalCount);
            Assert.Equal("EMPTY", (await query.GetAsync(new(EnterpriseSearch: "EMPTY", SiteSearch: "  "), default)).Items.Single().Code);
            Assert.Equal(2, (await query.GetAsync(filter with { ParentKind = EquipmentHierarchyKind.Site, ParentId = site.Id }, default)).Items.Single().ChildCount);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
            SqlConnection.ClearAllPools();
        }
    }
    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task TreePagesEachLevelKeepsSearchAncestorsAndExcludesDeletedChildren()
    {
        var source = Environment.GetEnvironmentVariable("OEE_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(source)) return;
        var connection = new SqlConnectionStringBuilder(source) { InitialCatalog = $"OeeDb_TreeTests_{Guid.NewGuid():N}" };
        Assert.StartsWith("OeeDb_TreeTests_", connection.InitialCatalog, StringComparison.Ordinal);
        var options = new DbContextOptionsBuilder<OeeDbContext>().UseSqlServer(connection.ConnectionString).Options;
        await using var db = new OeeDbContext(options);
        try
        {
            await db.Database.MigrateAsync();
            var enterprise = DomainTestData.Create<EnterpriseEntity>(("Code", "MCH"), ("Name", "Masan"));
            var site = DomainTestData.Create<SiteEntity>(("Enterprise", enterprise), ("Code", "MBP"), ("Name", "Beverage"), ("TimeZoneId", "UTC"));
            var area = DomainTestData.Create<AreaEntity>(("Site", site), ("Code", "CSD"), ("Name", "Packing"));
            var center = DomainTestData.Create<WorkCenterEntity>(("Area", area), ("Code", "WP2CSD"), ("Name", "Line"));
            var emptyCenter = DomainTestData.Create<WorkCenterEntity>(("Area", area), ("Code", "EMPTY"), ("Name", "Empty line"));
            var units = Enumerable.Range(1, 7).Select(i => DomainTestData.Create<WorkUnitEntity>(("WorkCenter", center), ("Code", $"UNIT{i:D2}"), ("Name", i == 4 ? "Filler" : $"Machine {i}"), ("OperationalState", OperationalStateEnum.Running))).ToArray();
            var deleted = DomainTestData.Create<WorkUnitEntity>(("WorkCenter", center), ("Code", "GHOST"), ("Name", "Deleted"), ("IsDeleted", true));
            db.AddRange(enterprise, site, area, center, emptyCenter, deleted,
                DomainTestData.Create<EnterpriseEntity>(("Code", "OTHER"), ("Name", "Other")));
            db.AddRange(units);
            await db.SaveChangesAsync();

            var query = new EquipmentHierarchyQueryService(options);
            var roots = await query.GetAsync(new(PageSize: 1), default);
            Assert.Equal(2, roots.TotalCount);
            Assert.Single(roots.Items);
            Assert.Equal(enterprise.Id, roots.Items.Single().Id);
            Assert.Equal("Active", roots.Items.Single().Status);
            Assert.Equal(1, roots.Items.Single().ChildCount);
            Assert.NotEmpty(Convert.FromBase64String(roots.Items.Single().RowVersion));
            Assert.Equal("OTHER", (await query.GetAsync(new(Page: 2, PageSize: 1), default)).Items.Single().Code);

            var sites = await query.GetAsync(new(EquipmentHierarchyKind.Enterprise, enterprise.Id), default);
            Assert.Equal((EquipmentHierarchyKind.Site, site.Id, (Guid?)enterprise.Id),
                (sites.Items.Single().Kind, sites.Items.Single().Id, sites.Items.Single().ParentId));
            var areas = await query.GetAsync(new(EquipmentHierarchyKind.Site, site.Id), default);
            Assert.Equal(2, areas.Items.Single().ChildCount);
            var centers = await query.GetAsync(new(EquipmentHierarchyKind.Area, area.Id), default);
            Assert.Equal(2, centers.TotalCount);
            Assert.Equal(0, centers.Items.Single(x => x.Id == emptyCenter.Id).ChildCount);
            Assert.Equal(7, centers.Items.Single(x => x.Id == center.Id).ChildCount);
            var page = await query.GetAsync(new(EquipmentHierarchyKind.WorkCenter, center.Id, Page: 2, PageSize: 3), default);
            Assert.Equal(7, page.TotalCount);
            Assert.Equal(ExpectedSecondPage, page.Items.Select(x => x.Code));
            Assert.All(page.Items, node =>
            {
                Assert.Equal(0, node.ChildCount);
                Assert.Equal("Active", node.Status);
                Assert.Equal("Running", node.OperationalState);
                Assert.Equal("NotConfigured", node.ConnectivityState);
            });

            // Tìm ở leaf vẫn trả đúng đường dẫn; số con là toàn bộ con thật, không phải số match.
            var foundRoot = await query.GetAsync(new(Search: "Filler"), default);
            Assert.Equal(enterprise.Id, foundRoot.Items.Single().Id);
            Assert.Equal(site.Id, (await query.GetAsync(new(EquipmentHierarchyKind.Enterprise, enterprise.Id, "Filler"), default)).Items.Single().Id);
            Assert.Equal(area.Id, (await query.GetAsync(new(EquipmentHierarchyKind.Site, site.Id, "Filler"), default)).Items.Single().Id);
            Assert.Equal(center.Id, (await query.GetAsync(new(EquipmentHierarchyKind.Area, area.Id, "Filler"), default)).Items.Single().Id);
            Assert.Equal(7, (await query.GetAsync(new(EquipmentHierarchyKind.Area, area.Id, "Filler"), default)).Items.Single().ChildCount);
            Assert.Equal("UNIT04", (await query.GetAsync(new(EquipmentHierarchyKind.WorkCenter, center.Id, "Filler"), default)).Items.Single().Code);
            Assert.Empty((await query.GetAsync(new(Search: "GHOST"), default)).Items);
            Assert.Equal(7, (await query.GetAsync(new(EquipmentHierarchyKind.WorkCenter, center.Id, "MCH"), default)).TotalCount);
            await Assert.ThrowsAsync<ArgumentException>(() => query.GetAsync(new(EquipmentHierarchyKind.WorkUnit, units[0].Id), default));
            await Assert.ThrowsAsync<ArgumentException>(() => query.GetAsync(new(ParentId: enterprise.Id), default));

            // Status int vẫn trả nhãn đúng cho Tree khi đọc lại từ SQL.
            DomainTestData.Set(enterprise, "Status", 0);
            DomainTestData.Set(units[0], "Status", 0);
            await db.SaveChangesAsync();
            Assert.Equal("Inactive", (await query.GetAsync(new(Search: "MCH"), default)).Items.Single().Status);
            var updatedUnits = await query.GetAsync(new(EquipmentHierarchyKind.WorkCenter, center.Id), default);
            Assert.Equal("Inactive", updatedUnits.Items.Single(x => x.Id == units[0].Id).Status);
            Assert.Equal("Active", updatedUnits.Items.Single(x => x.Id == units[1].Id).Status);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
}
