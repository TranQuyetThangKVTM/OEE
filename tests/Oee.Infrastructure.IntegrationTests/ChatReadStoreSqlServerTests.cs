using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Oee.Application.Chat;
using Oee.Application.Common;
using Oee.Domain.Common;
using Oee.Domain.Entities.Oee;
using Oee.Domain.Entities.Organization;
using Oee.Domain.Entities.Production;
using Oee.Domain.Platform;
using Oee.Infrastructure.Chat;
using Oee.Infrastructure.Persistence;
using Oee.Tests.Fixtures;

namespace Oee.Infrastructure.IntegrationTests;

public sealed class ChatReadStoreSqlServerTests
{
    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task ProjectionsTranslateAndKeepFullPlanTotalsAndCorrectTargetScope()
    {
        var connectionString = Environment.GetEnvironmentVariable("OEE_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        var connection = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = $"OeeDb_ChatTests_{Guid.NewGuid():N}" };
        await using var db = new OeeDbContext(new DbContextOptionsBuilder<OeeDbContext>().UseSqlServer(connection.ConnectionString).Options);
        var at = new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero);
        try
        {
            await db.Database.MigrateAsync();
            var enterprise = EnterpriseEntity.Create("CHAT-E", "Enterprise");
            var site = SiteEntity.Create(enterprise.Id, "CHAT-S", "Site", "Asia/Ho_Chi_Minh");
            var area = AreaEntity.Create(site.Id, "CHAT-A", "Area");
            var group = WorkCenterEntity.Create(area.Id, "CHAT-G", "Group");
            var a = WorkUnitEntity.Create(group.Id, "M01", "Machine A");
            var b = WorkUnitEntity.Create(group.Id, "M02", "Machine B");
            var product = ProductEntity.Create("SKU-CHAT", "Product", "unit");
            var p1 = ProductionPlanEntity.Create(a.Id, product.Id, "CHAT-P1", at, at.AddHours(8), 100);
            var p2 = ProductionPlanEntity.Create(a.Id, product.Id, "CHAT-P2", at.AddHours(8), at.AddHours(16), 200);
            db.AddRange(enterprise, site, area, group, a, b, product, p1, p2);
            db.Add(ProductionOutputEntity.Create(p1.Id, a.Id, product.Id, at, at.AddHours(1), 10, 150, DataSourceEnum.Manual));
            db.Add(ProductionOutputEntity.Create(p2.Id, a.Id, product.Id, at.AddHours(8), at.AddHours(9), 10, 20, DataSourceEnum.Manual));
            var today = new DateOnly(2026, 10, 26);
            db.Add(OeeTargetEntity.Create(OeePeriodTypeEnum.Day, "DAY", .8m, .8m, .8m, .8m, today));
            db.Add(OeeTargetEntity.Create(OeePeriodTypeEnum.Day, "DAY", .81m, .8m, .8m, .8m, today, siteId: site.Id));
            db.Add(OeeTargetEntity.Create(OeePeriodTypeEnum.Day, "DAY", .82m, .8m, .8m, .8m, today, workUnitId: a.Id, effectiveTo: today.AddDays(3)));
            db.Add(OeeTargetEntity.Create(OeePeriodTypeEnum.Day, "DAY", .83m, .8m, .8m, .8m, today, workUnitId: b.Id));
            db.Add(AuditLogEntity.Create(at, "Update", "Product", product.Id.ToString(), userName: "tester", changes: "{\"sentinel\":\"not-for-chat\"}"));
            var snapshot = DomainTestData.Create<OeeSnapshotEntity>(("WorkUnitId", a.Id), ("PeriodType", OeePeriodTypeEnum.Day),
                ("PeriodCode", "DAY-20261026"), ("PeriodStart", at), ("PeriodEnd", at.AddDays(1)), ("CalculatedAt", at), ("CalculationVersion", "CHAT-TEST"));
            db.Add(snapshot);
            foreach (var metric in new[] { AlertMetricEnum.Oee, AlertMetricEnum.Availability, AlertMetricEnum.Performance })
                db.Add(OeeAlertEntity.Create(snapshot.Id, a.Id, metric, AlertSeverityEnum.Warning, .7m, .8m, at));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var store = new EfChatReadStore(db, new Clock(at));
            foreach (var kind in Enum.GetValues<ChatCatalogKind>())
            {
                var page = await store.CatalogAsync(kind, a.Id, at, at.AddDays(1), today, 1, default);
                Assert.True(page.TotalCount >= page.Items.Count);
                Assert.DoesNotContain("not-for-chat", JsonSerializer.Serialize(page), StringComparison.Ordinal);
            }
            var plans = await store.PlansAsync(a.Id, at, at.AddDays(1), false, 1, default);
            Assert.Equal(2, plans.TotalCount);
            Assert.Equal(300, plans.Planned);
            Assert.Equal(170, plans.Actual);
            Assert.Equal(1.5m, Assert.Single(plans.Items).Attainment);
            var targets = await store.TargetsAsync(a.Id, today, 20, default);
            Assert.Equal(3, targets.TotalCount);
            Assert.DoesNotContain(targets.Items, target => target.WorkUnitId == b.Id);
            var expiring = await store.TargetsAsync(a.Id, today, 20, default, today.AddDays(30));
            Assert.Equal(a.Id, Assert.Single(expiring.Items).WorkUnitId);
            var alertStats = await store.CatalogAsync(ChatCatalogKind.AlertStatistics, a.Id, at, at.AddDays(1), today, 1, default);
            Assert.Equal(3, alertStats.TotalEvents);
            Assert.Single(alertStats.Items);
            var unmapped = await store.CatalogAsync(ChatCatalogKind.UnmappedWorkUnit, a.Id, at, at.AddDays(1), today, 20, default);
            Assert.Equal("M01", Assert.Single(unmapped.Items).Code);
            Assert.Empty(db.ChangeTracker.Entries());
        }
        finally
        {
            Assert.StartsWith("OeeDb_ChatTests_", connection.InitialCatalog, StringComparison.Ordinal);
            await db.Database.EnsureDeletedAsync();
            SqlConnection.ClearAllPools();
        }
    }

    private sealed class Clock(DateTimeOffset at) : IClock { public DateTimeOffset UtcNow => at; }
}
