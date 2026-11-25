using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Oee.Application.Common;
using Oee.Domain.Common;
using Oee.Domain.Downtime;
using Oee.Domain.Entities.Oee;
using Oee.Domain.Entities.Organization;
using Oee.Domain.Entities.Production;
using Oee.Infrastructure.BackgroundServices;
using Oee.Infrastructure.BackgroundServices.Configuration;
using Oee.Infrastructure.Persistence;
using Oee.Tests.Fixtures;

namespace Oee.Infrastructure.IntegrationTests;

public sealed class OeeSnapshotElapsedSqlServerTests
{
    private static readonly DateTimeOffset ShiftStart = new(2026, 8, 1, 22, 0, 0, TimeSpan.FromHours(7));

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task ActiveShiftUsesElapsedTimeAndRecalculatesWhenTimePassesWithoutNewQuantity()
    {
        var source = Environment.GetEnvironmentVariable("OEE_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(source)) return;
        var connection = TestConnection(source);
        await using var db = CreateDb(connection);
        try
        {
            await db.Database.MigrateAsync();
            var seed = await SeedAsync(db);
            var clock = new MutableClock { UtcNow = ShiftStart.AddMinutes(10) };
            var service = ApplicationServiceFactory.Snapshots(db, clock);
            var id = await service.RecalculateAsync(seed.Unit.Id, OeePeriodTypeEnum.Shift, "SHIFT-S3-20260801",
                ShiftStart, ShiftStart.AddHours(8), CancellationToken.None);
            var first = await SnapshotAsync(db, id);
            Assert.Equal(10, first.LoadingMinutes);
            Assert.Equal(1000, first.TotalQuantity); // Future fact and previous-period boundary fact are excluded.
            Assert.Equal(1, first.Availability);
            Assert.Equal(1, first.Performance);
            Assert.Equal(.99m, first.Quality);
            Assert.Equal(.99m, first.Oee);
            Assert.Equal(10, first.NetUtilizationMinutes);
            Assert.Equal(9.9m, first.ValueAddedMinutes);
            Assert.Equal(ShiftStart.AddHours(8), first.PeriodEnd); // Live query still identifies the current shift.

            clock.UtcNow = ShiftStart.AddMinutes(20);
            Assert.Equal(id, await service.RecalculateAsync(seed.Unit.Id, OeePeriodTypeEnum.Shift, "SHIFT-S3-20260801",
                ShiftStart, ShiftStart.AddHours(8), CancellationToken.None));
            var later = await SnapshotAsync(db, id);
            Assert.Equal(20, later.LoadingMinutes);
            Assert.Equal(1000, later.TotalQuantity);
            Assert.Equal(.5m, later.Performance);
            Assert.Equal(.495m, later.Oee);
            Assert.Equal(10, later.PerformanceLossMinutes);
            Assert.NotEqual(first.InputHash, later.InputHash);
            Assert.Equal(clock.UtcNow, later.CalculatedAt);
            await service.RecalculateAsync(seed.Unit.Id, OeePeriodTypeEnum.Shift, "SHIFT-S3-20260801",
                ShiftStart, ShiftStart.AddHours(8), CancellationToken.None);
            Assert.Equal(later.InputHash, (await SnapshotAsync(db, id)).InputHash);
            Assert.Equal(1, await db.OeeSnapshots.CountAsync());

            var stop = DomainTestData.Create<DowntimeEventEntity>(("WorkUnitId", seed.Unit.Id), ("DowntimeReasonId", seed.Reason.Id), ("StartAt", ShiftStart.AddMinutes(20)), ("Category", DowntimeCategoryEnum.AvailabilityLoss), ("Kind", DowntimeKindEnum.Breakdown), ("Source", DataSourceEnum.Automatic));
            db.DowntimeEvents.Add(stop);
            await db.SaveChangesAsync();
            clock.UtcNow = ShiftStart.AddMinutes(30);
            await service.RecalculateAsync(seed.Unit.Id, OeePeriodTypeEnum.Shift, "SHIFT-S3-20260801",
                ShiftStart, ShiftStart.AddHours(8), CancellationToken.None);
            var stopped = await SnapshotAsync(db, id);
            Assert.Equal(10, stopped.AvailabilityLossMinutes); // Open stop ends at now, not shift end.
            Assert.Equal(20, stopped.UtilizationMinutes);
            Assert.Equal(.5m, stopped.Performance);
            Assert.Equal(.33m, stopped.Oee);

            // Reclassification at the same clock must update A/P instead of being skipped by InputHash.
            var persistedStop = await db.DowntimeEvents.SingleAsync(x => x.Id == stop.Id);
            DomainTestData.Set(persistedStop, "Category", DowntimeCategoryEnum.PerformanceLoss);
            await db.SaveChangesAsync();
            await service.RecalculateAsync(seed.Unit.Id, OeePeriodTypeEnum.Shift, "SHIFT-S3-20260801",
                ShiftStart, ShiftStart.AddHours(8), CancellationToken.None);
            var reclassified = await SnapshotAsync(db, id);
            Assert.NotEqual(stopped.InputHash, reclassified.InputHash);
            Assert.Equal(1, reclassified.Availability);
            Assert.Equal(0, reclassified.AvailabilityLossMinutes);
            Assert.Equal(30, reclassified.UtilizationMinutes);

            clock.UtcNow = ShiftStart.AddHours(9);
            await service.RecalculateAsync(seed.Unit.Id, OeePeriodTypeEnum.Shift, "SHIFT-S3-20260801",
                ShiftStart, ShiftStart.AddHours(8), CancellationToken.None);
            var complete = await SnapshotAsync(db, id);
            Assert.Equal(480, complete.TotalMinutes);
            Assert.Equal(480, complete.LoadingMinutes);
            Assert.Equal(6000, complete.TotalQuantity);
            Assert.Equal(ShiftStart.AddHours(8), complete.PeriodEnd);
            await Assert.ThrowsAsync<ArgumentException>(() => service.RecalculateAsync(seed.Unit.Id,
                OeePeriodTypeEnum.Shift, "FUTURE", clock.UtcNow.AddHours(1), clock.UtcNow.AddHours(9), CancellationToken.None));
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
            SqlConnection.ClearAllPools();
        }
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task SnapshotWorkerStartsAndKeepsRecalculatingWithoutAnyBrowserOrHttpRequest()
    {
        var source = Environment.GetEnvironmentVariable("OEE_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(source)) return;
        var connection = TestConnection(source);
        await using var db = CreateDb(connection);
        try
        {
            await db.Database.MigrateAsync();
            var seed = await SeedAsync(db);
            var clock = new MutableClock { UtcNow = ShiftStart.AddMinutes(10) };
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IClock>(clock);
            services.AddOeeInfrastructure(connection.ConnectionString, includeSignInManager: false, includeIdentityStore: false);
            await using var provider = services.BuildServiceProvider();
            using var worker = new OeeSnapshotWorker(provider.GetRequiredService<IServiceScopeFactory>(),
                Options.Create(new OeeProcessingOptions { SnapshotSeconds = 5, SnapshotLookbackHours = 6 }),
                NullLogger<OeeSnapshotWorker>.Instance);
            await worker.StartAsync(CancellationToken.None);
            try
            {
                var first = await WaitForShiftAsync(db, seed.Unit.Id, clock.UtcNow);
                Assert.Equal(1, first.Performance);
                Assert.Equal(10, first.LoadingMinutes);
                clock.UtcNow = ShiftStart.AddMinutes(20);
                var second = await WaitForShiftAsync(db, seed.Unit.Id, clock.UtcNow);
                Assert.Equal(.5m, second.Performance);
                Assert.Equal(20, second.LoadingMinutes);
                Assert.Equal(first.Id, second.Id);
                Assert.False(await db.OeeSnapshots.AnyAsync(x => x.WorkUnitId == seed.FutureUnit.Id));
                Assert.False(await db.OeeSnapshots.AnyAsync(x => x.PeriodStart >= clock.UtcNow));
            }
            finally
            {
                await worker.StopAsync(CancellationToken.None);
            }

            // Midnight has an active overnight shift but the new DAY has not elapsed yet.
            clock.UtcNow = ShiftStart.AddHours(2);
            await ApplicationServiceFactory.Snapshots(db, clock).RecalculateLookbackAsync(TimeSpan.FromHours(6), CancellationToken.None);
            Assert.False(await db.OeeSnapshots.AnyAsync(x => x.PeriodStart >= clock.UtcNow));
            var midnight = await db.OeeSnapshots.AsNoTracking().SingleAsync(x => x.WorkUnitId == seed.Unit.Id
                && x.PeriodCode == "SHIFT-S3-20260801");
            Assert.Equal(120, midnight.LoadingMinutes);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
            SqlConnection.ClearAllPools();
        }
    }

    private static SqlConnectionStringBuilder TestConnection(string source)
    {
        var connection = new SqlConnectionStringBuilder(source) { InitialCatalog = $"OeeDb_Elapsed_{Guid.NewGuid():N}" };
        Assert.StartsWith("OeeDb_Elapsed_", connection.InitialCatalog, StringComparison.Ordinal);
        return connection;
    }

    private static OeeDbContext CreateDb(SqlConnectionStringBuilder connection) =>
        new(new DbContextOptionsBuilder<OeeDbContext>().UseSqlServer(connection.ConnectionString).Options);

    private static async Task<OeeSnapshotEntity> SnapshotAsync(OeeDbContext db, Guid id)
    {
        db.ChangeTracker.Clear();
        return await db.OeeSnapshots.AsNoTracking().SingleAsync(x => x.Id == id);
    }

    private static async Task<OeeSnapshotEntity> WaitForShiftAsync(OeeDbContext db, Guid unitId, DateTimeOffset calculatedAt)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        while (true)
        {
            var snapshot = await db.OeeSnapshots.AsNoTracking().SingleOrDefaultAsync(x => x.WorkUnitId == unitId
                && x.PeriodCode == "SHIFT-S3-20260801" && x.CalculatedAt >= calculatedAt, timeout.Token);
            if (snapshot is not null) return snapshot;
            await Task.Delay(50, timeout.Token);
        }
    }

    private static async Task<Seed> SeedAsync(OeeDbContext db)
    {
        var enterprise = DomainTestData.Create<EnterpriseEntity>(("Code", "E"), ("Name", "Enterprise"), ("Status", 1));
        var site = DomainTestData.Create<SiteEntity>(("Enterprise", enterprise), ("Code", "S"), ("Name", "Site"), ("TimeZoneId", "Asia/Ho_Chi_Minh"), ("Status", 1));
        var area = DomainTestData.Create<AreaEntity>(("Site", site), ("Code", "A"), ("Name", "Area"), ("Status", 1));
        var center = DomainTestData.Create<WorkCenterEntity>(("Area", area), ("Code", "C"), ("Name", "Center"), ("Status", 1));
        var unit = DomainTestData.Create<WorkUnitEntity>(("WorkCenter", center), ("Code", "M1"), ("Name", "Machine"), ("Status", 1));
        var futureUnit = DomainTestData.Create<WorkUnitEntity>(("WorkCenter", center), ("Code", "MF"), ("Name", "Future machine"), ("Status", 1));
        var product = DomainTestData.Create<ProductEntity>(("Code", "SKU"), ("Name", "Product"), ("DefaultUom", "unit"), ("Status", 1));
        var shift = DomainTestData.Create<ShiftEntity>(("Site", site), ("Code", "S3"), ("Name", "Night"), ("StartTime", (global::System.TimeOnly)(new(22, 0))), ("EndTime", (global::System.TimeOnly)(new(6, 0))), ("Status", 1));
        var laterShift = DomainTestData.Create<ShiftEntity>(("Site", site), ("Code", "S1"), ("Name", "Morning"), ("StartTime", (global::System.TimeOnly)(new(6, 0))), ("EndTime", (global::System.TimeOnly)(new(14, 0))), ("Status", 1));
        var reason = DomainTestData.Create<DowntimeReasonEntity>(("Code", "FAIL"), ("Name", "Failure"), ("Category", DowntimeCategoryEnum.AvailabilityLoss), ("Kind", DowntimeKindEnum.Breakdown), ("Status", 1));
        var plan = Plan(unit, product, shift, "P", ShiftStart, ShiftStart.AddHours(8));
        var oldPlan = Plan(unit, product, shift, "OLD", ShiftStart.AddHours(-8), ShiftStart);
        var futurePlan = Plan(futureUnit, product, shift, "PF", ShiftStart, ShiftStart.AddHours(8));
        db.AddRange(enterprise, site, area, center, unit, futureUnit, product, shift, laterShift, reason, plan, oldPlan, futurePlan);
        db.ProductionOutputs.AddRange(
            Output(plan, ShiftStart, ShiftStart.AddMinutes(10), 1000, 10),
            Output(oldPlan, ShiftStart.AddMinutes(-20), ShiftStart, 500, 0),
            Output(plan, ShiftStart.AddMinutes(60), ShiftStart.AddMinutes(70), 5000, 0),
            Output(futurePlan, ShiftStart.AddMinutes(60), ShiftStart.AddMinutes(70), 500, 0));
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return new Seed(unit, futureUnit, reason);
    }

    private static ProductionPlanEntity Plan(WorkUnitEntity unit, ProductEntity product, ShiftEntity shift, string code,
        DateTimeOffset start, DateTimeOffset end)
    {
        var plan = DomainTestData.Create<ProductionPlanEntity>(("WorkUnit", unit), ("Product", product), ("Shift", shift), ("Code", code), ("PlannedStart", start), ("PlannedEnd", end), ("PlannedQuantity", 48000));
        DomainTestData.Set(plan, "VersionSeriesId", plan.Id);
        return plan;
    }

    private static ProductionOutputEntity Output(ProductionPlanEntity plan, DateTimeOffset start, DateTimeOffset end,
        decimal total, decimal defect) => DomainTestData.Create<ProductionOutputEntity>(("ProductionPlan", plan), ("WorkUnit", plan.WorkUnit), ("Product", plan.Product), ("Shift", plan.Shift), ("PeriodStart", start), ("PeriodEnd", end), ("TotalProducedQuantity", total), ("DefectQuantity", defect), ("StandardSpeedPerMinute", 100), ("Source", DataSourceEnum.Automatic));

    private sealed record Seed(WorkUnitEntity Unit, WorkUnitEntity FutureUnit, DowntimeReasonEntity Reason);

    private sealed class MutableClock : IClock
    {
        private readonly object gate = new();
        private DateTimeOffset utcNow;
        public DateTimeOffset UtcNow { get { lock (gate) return utcNow; } set { lock (gate) utcNow = value; } }
    }
}
