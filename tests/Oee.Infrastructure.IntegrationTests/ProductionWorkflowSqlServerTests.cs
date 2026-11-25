using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Oee.Application.Common;
using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.Operations;
using Oee.Application.Security;
using Oee.Domain.Common;
using Oee.Domain.Entities.Organization;
using Oee.Domain.Entities.Production;
using Oee.Infrastructure.Audit;
using Oee.Infrastructure.Operations;
using Oee.Infrastructure.Persistence;
using Oee.Tests.Fixtures;

namespace Oee.Infrastructure.IntegrationTests;

/// <summary>
/// Integration Test SQL Server cho kế hoạch, fact sản lượng lịch sử, downtime và kế hoạch mẫu.
/// </summary>
public sealed class ProductionWorkflowSqlServerTests
{
    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task ProductionWorkflowPreventsDuplicatesAndAuditsReclassification()
    {
        // Arrange: database riêng được migrate và seed master/standard trước khi chạy use case production.
        var baseConnectionString = Environment.GetEnvironmentVariable("OEE_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(baseConnectionString)) return;

        var databaseName = $"OeeDb_ProductionTests_{Guid.NewGuid():N}";
        var connection = new SqlConnectionStringBuilder(baseConnectionString) { InitialCatalog = databaseName };
        Assert.StartsWith("OeeDb_ProductionTests_", connection.InitialCatalog, StringComparison.Ordinal);
        var options = new DbContextOptionsBuilder<OeeDbContext>()
            .UseSqlServer(connection.ConnectionString)
            .AddInterceptors(new AuditSaveChangesInterceptor(new TestActor()))
            .Options;
        await using var db = new OeeDbContext(options);
        try
        {
            await db.Database.MigrateAsync();
            var clock = new FixedClock();
            var seed = await SeedAsync(db);
            var planService = ApplicationServiceFactory.Plans(db, clock);
            var downtimeService = ApplicationServiceFactory.Downtime(db, clock);
            var sampleService = ApplicationServiceFactory.Samples(db, clock);
            var query = new ProductionModuleQueryService(db, clock);

            // Fixture fact lịch sử để kiểm tra bảo toàn dữ liệu khi sửa/xóa/versioning kế hoạch.
            var planRequest = new ProductionPlanRequest(seed.WorkUnit.Id, seed.Product.Id, seed.Shift.Id, "PLAN-001",
                clock.UtcNow.AddHours(-8), clock.UtcNow.AddHours(8), 1_000m, "integration");
            var plan = await planService.CreateAsync(planRequest, CancellationToken.None);
            Assert.Null(await db.ProductionPlans.Where(x => x.Id == plan.Id).Select(x => x.DeletedAt).SingleAsync());
            await Assert.ThrowsAsync<InvalidOperationException>(() => planService.CreateAsync(planRequest with { Code = "OVERLAP" }, CancellationToken.None));
            var historicOutput = ProductionOutputEntity.Create(plan.Id, seed.WorkUnit.Id, seed.Product.Id,
                clock.UtcNow.AddHours(-1), clock.UtcNow, 10m, 100m, DataSourceEnum.Manual, 5m, seed.Shift.Id);
            db.ProductionOutputs.Add(historicOutput);
            await db.SaveChangesAsync();
            var outputId = historicOutput.Id;

            // Act & Assert: plan đã có output chỉ được correction/versioning, không sửa trực tiếp.
            await Assert.ThrowsAsync<InvalidOperationException>(() => planService.UpdateAsync(plan.Id, planRequest,
                Convert.FromBase64String(plan.RowVersion), CancellationToken.None));
            var correction = await planService.CorrectAsync(plan.Id, planRequest with { Code = "PLAN-001-V2" },
                Convert.FromBase64String(plan.RowVersion), CancellationToken.None);
            Assert.Equal(2, correction.Version);
            Assert.Equal(plan.Id, await db.ProductionPlans.Where(x => x.Id == correction.Id).Select(x => x.ReplacesPlanId).SingleAsync());
            await Assert.ThrowsAsync<KeyNotFoundException>(() => planService.GetAsync(plan.Id, CancellationToken.None));
            await Assert.ThrowsAsync<KeyNotFoundException>(() => planService.CorrectAsync(plan.Id, planRequest,
                Convert.FromBase64String(plan.RowVersion), CancellationToken.None));

            // Xóa chỉ ghi thời điểm; bản gốc và sản lượng lịch sử còn nguyên.
            var cancelledPlan = await planService.CreateAsync(planRequest with { Code = "PLAN-CANCEL",
                PlannedStart = planRequest.PlannedEnd, PlannedEnd = planRequest.PlannedEnd.AddHours(1) }, CancellationToken.None);
            await Assert.ThrowsAsync<ConcurrencyConflictException>(() => planService.DeleteAsync(cancelledPlan.Id, [0], CancellationToken.None));
            Assert.Null(await db.ProductionPlans.AsNoTracking().Where(x => x.Id == cancelledPlan.Id).Select(x => x.DeletedAt).SingleAsync());
            await planService.DeleteAsync(cancelledPlan.Id, Convert.FromBase64String(cancelledPlan.RowVersion), CancellationToken.None);
            Assert.Equal(clock.UtcNow,
                await db.ProductionPlans.Where(x => x.Id == cancelledPlan.Id).Select(x => x.DeletedAt).SingleAsync());
            await Assert.ThrowsAsync<KeyNotFoundException>(() => planService.GetAsync(cancelledPlan.Id, CancellationToken.None));
            var visible = await query.PlansAsync(new ProductionPlanListQuery(Descending: false), CancellationToken.None);
            Assert.DoesNotContain(visible.Items, x => x.Id == cancelledPlan.Id || x.Id == plan.Id);
            Assert.Equal(1, await db.ProductionOutputs.CountAsync(x => x.Id == outputId));

            // Act & Assert: reclassify giữ nguyên time window, có audit và chặn token cũ.
            var start = clock.UtcNow.AddHours(-4);
            var end = clock.UtcNow.AddHours(-3);
            var downtimeId = await downtimeService.CreateAsync(new DowntimeRequest(seed.WorkUnit.Id, seed.UnknownReason.Id,
                start, end, null, "unknown"), CancellationToken.None);
            var downtime = await query.DowntimeAsync(downtimeId, CancellationToken.None);
            await downtimeService.ReclassifyAsync(downtimeId, seed.BreakdownReason.Id, downtime.ConcurrencyToken, CancellationToken.None);
            var reclassified = await query.DowntimeAsync(downtimeId, CancellationToken.None);
            Assert.Equal(start, reclassified.StartAt);
            Assert.Equal(end, reclassified.EndAt);
            Assert.Equal(seed.BreakdownReason.Id, reclassified.DowntimeReasonId);
            Assert.Contains(await db.AuditLogs.AsNoTracking().Where(x => x.EntityType == "DowntimeEvent").ToArrayAsync(),
                log => log.Action == "Modified" &&
                    log.Changes?.Contains("DowntimeReasonId", StringComparison.Ordinal) == true);
            await Assert.ThrowsAsync<ConcurrencyConflictException>(() => downtimeService.ReclassifyAsync(downtimeId,
                seed.UnknownReason.Id, downtime.ConcurrencyToken, CancellationToken.None));

            // Act & Assert: downtime đang mở lưu EndAt null và được đóng bằng command có concurrency token.
            db.ChangeTracker.Clear();
            var openId = await downtimeService.CreateAsync(new DowntimeRequest(seed.WorkUnit.Id, seed.BreakdownReason.Id,
                clock.UtcNow.AddHours(1), null, null, "open"), CancellationToken.None);
            var open = await query.DowntimeAsync(openId, CancellationToken.None);
            Assert.Null(open.EndAt);
            await downtimeService.CloseAsync(openId, clock.UtcNow.AddHours(2), open.ConcurrencyToken, CancellationToken.None);
            Assert.NotNull((await query.DowntimeAsync(openId, CancellationToken.None)).EndAt);

            // Act & Assert: mẫu hỗ trợ thêm dòng, query detail, update và optimistic concurrency.
            db.ChangeTracker.Clear();
            var sample = await sampleService.CreateAsync(new SamplePlanRequest("SAMPLE-001", "Sample", null), CancellationToken.None);
            var addedItems = await sampleService.AddItemsAsync(sample.Id, new SamplePlanBulkAddItemsRequest([seed.WorkUnit.Id],
                [seed.Shift.Id], seed.Product.Id, 500m, "item"), CancellationToken.None);
            Assert.Single(addedItems.CreatedItems);
            var samplePage = await query.SamplesAsync(new SamplePlanListQuery("SAMPLE", PageSize: 10), CancellationToken.None);
            Assert.Single(samplePage.Items);
            var sampleDetail = await query.SampleAsync(sample.Id, CancellationToken.None);
            Assert.Equal(1, sampleDetail.ItemCount);
            Assert.Equal(500m, sampleDetail.TotalPlannedQuantity);
            var updatedSample = await sampleService.UpdateAsync(sample.Id, new SamplePlanRequest("SAMPLE-001", "Updated", null),
                Convert.FromBase64String(sampleDetail.RowVersion), CancellationToken.None);
            await Assert.ThrowsAsync<ConcurrencyConflictException>(() => sampleService.UpdateAsync(sample.Id,
                new SamplePlanRequest("SAMPLE-001", "Stale", null), Convert.FromBase64String(sampleDetail.RowVersion), CancellationToken.None));
            Assert.NotEqual(sampleDetail.RowVersion, updatedSample.RowVersion);

            // Act & Assert: sinh plan từ mẫu theo ngày là idempotent; lần chạy thứ hai không tạo thêm.
            db.ChangeTracker.Clear();
            var generateRequest = new GenerateSamplePlanRequest(sample.Id, new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 3));
            Assert.Equal(1, await planService.GenerateFromSampleAsync(generateRequest, CancellationToken.None));
            Assert.Equal(0, await planService.GenerateFromSampleAsync(generateRequest, CancellationToken.None));

        }
        finally
        {
            // Cleanup database test tách biệt.
            await db.Database.EnsureDeletedAsync();
            SqlConnection.ClearAllPools();
        }
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task PlanExecutionStatusSortsBeforePagingAndAggregatesAllOutputs()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable("OEE_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(baseConnectionString)) return;
        var connection = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = $"OeeDb_PlanExecutionTests_{Guid.NewGuid():N}"
        };
        await using var db = new OeeDbContext(new DbContextOptionsBuilder<OeeDbContext>()
            .UseSqlServer(connection.ConnectionString).Options);
        try
        {
            await db.Database.MigrateAsync();
            var seed = await SeedAsync(db);
            var clock = new FixedClock();
            var pending = AddPlan("PENDING", 1, 8);
            var running = AddPlan("RUNNING", -8, 1);
            var missing = AddPlan("MISSING", -16, -8);
            var under = AddPlan("UNDER", -24, -16);
            var onTarget = AddPlan("ON-TARGET", -32, -24);
            AddOutput(under, 20m);
            AddOutput(under, 30m);
            AddOutput(onTarget, 50m);
            AddOutput(onTarget, 75m);
            await db.SaveChangesAsync();

            var query = new ProductionModuleQueryService(db, clock);
            var first = await query.PlansAsync(new ProductionPlanListQuery(SortBy: "status", Descending: false, PageSize: 2), CancellationToken.None);
            Assert.Equal(5, first.TotalCount);
            Assert.Equal(new[] { pending.Id, running.Id }, first.Items.Select(x => x.Id));
            Assert.Equal(new[] { ProductionPlanExecutionStatusEnum.Pending, ProductionPlanExecutionStatusEnum.InProgress }, first.Items.Select(x => x.ExecutionStatus));
            var second = await query.PlansAsync(new ProductionPlanListQuery(SortBy: "status", Descending: false, Page: 2, PageSize: 2), CancellationToken.None);
            Assert.Equal(new[] { missing.Id, under.Id }, second.Items.Select(x => x.Id));
            Assert.Collection(second.Items,
                item => Assert.Equal(ProductionPlanExecutionStatusEnum.MissingOutput, item.ExecutionStatus),
                item =>
                {
                    Assert.Equal(ProductionPlanExecutionStatusEnum.CompletedUnderTarget, item.ExecutionStatus);
                    Assert.Equal(50m, item.ActualQuantity);
                });
            var reverse = await query.PlansAsync(new ProductionPlanListQuery(SortBy: "status", Descending: true, PageSize: 1), CancellationToken.None);
            var achieved = Assert.Single(reverse.Items);
            Assert.Equal(onTarget.Id, achieved.Id);
            Assert.Equal(ProductionPlanExecutionStatusEnum.CompletedOnTarget, achieved.ExecutionStatus);
            Assert.Equal(1.25m, achieved.Attainment);

            // Bản ghi 0 là hụt kế hoạch; không được đánh đồng với chưa từng ghi nhận output.
            AddOutput(missing, 0m);
            await db.SaveChangesAsync();
            var recordedZero = Assert.Single((await query.PlansAsync(new ProductionPlanListQuery(Search: "MISSING"), CancellationToken.None)).Items);
            Assert.True(recordedZero.HasOutput);
            Assert.Equal(ProductionPlanExecutionStatusEnum.CompletedUnderTarget, recordedZero.ExecutionStatus);

            ProductionPlanEntity AddPlan(string code, int start, int end)
            {
                var plan = DomainTestData.Create<ProductionPlanEntity>(("Code", code), ("WorkUnitId", seed.WorkUnit.Id), ("ProductId", seed.Product.Id), ("ShiftId", seed.Shift.Id), ("PlannedStart", clock.UtcNow.AddHours(start)), ("PlannedEnd", clock.UtcNow.AddHours(end)), ("PlannedQuantity", 100m), ("VersionSeriesId", Guid.NewGuid()));
                db.ProductionPlans.Add(plan);
                return plan;
            }

            void AddOutput(ProductionPlanEntity plan, decimal actual) => db.ProductionOutputs.Add(DomainTestData.Create<ProductionOutputEntity>(("ProductionPlan", plan), ("WorkUnitId", seed.WorkUnit.Id), ("ProductId", seed.Product.Id), ("ShiftId", seed.Shift.Id), ("PeriodStart", plan.PlannedStart), ("PeriodEnd", plan.PlannedEnd), ("StandardSpeedPerMinute", 10m), ("TotalProducedQuantity", actual), ("Source", DataSourceEnum.Manual)));
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
            SqlConnection.ClearAllPools();
        }
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task LegacyPlansStayCompatibleAndRemovedVersionsKeepOutputHistory()
    {
        var baseConnectionString = Environment.GetEnvironmentVariable("OEE_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(baseConnectionString)) return;
        var connection = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = $"OeeDb_PlanCompatibilityTests_{Guid.NewGuid():N}"
        };
        await using var db = new OeeDbContext(new DbContextOptionsBuilder<OeeDbContext>()
            .UseSqlServer(connection.ConnectionString).Options);
        try
        {
            // Áp dụng migration hiện hữu, không tạo schema riêng cho các quy tắc mới.
            await db.Database.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges());
            var seed = await SeedAsync(db);
            var clock = new FixedClock();
            var plans = Enumerable.Range(0, 6).Select(index => DomainTestData.Create<ProductionPlanEntity>(("Code", $"COMPAT-{index}"), ("VersionSeriesId", Guid.NewGuid()), ("WorkUnitId", seed.WorkUnit.Id), ("ProductId", seed.Product.Id), ("ShiftId", seed.Shift.Id), ("PlannedStart", clock.UtcNow.AddHours(-1)), ("PlannedEnd", clock.UtcNow.AddHours(1)), ("PlannedQuantity", 100))).ToArray();
            DomainTestData.Set(plans[5], "ReplacesPlanId", plans[4].Id);
            DomainTestData.Set(plans[5], "DeletedAt", clock.UtcNow);
            db.AddRange(plans);
            foreach (var index in new[] { 2, 4 })
                db.ProductionOutputs.Add(DomainTestData.Create<ProductionOutputEntity>(("ProductionPlan", plans[index]), ("WorkUnitId", seed.WorkUnit.Id), ("ProductId", seed.Product.Id), ("PeriodStart", clock.UtcNow.AddMinutes(-30)), ("PeriodEnd", clock.UtcNow), ("StandardSpeedPerMinute", 10), ("TotalProducedQuantity", index + 5), ("Source", DataSourceEnum.Manual)));
            await db.SaveChangesAsync();
            // Mô phỏng database cũ còn cả giá trị 1/2/3/4, kể cả bản cũ thiếu thời điểm xóa/liên kết thay thế.
            await db.ProductionPlans.Where(x => x.Id == plans[1].Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => EF.Property<int>(x, "Status"), 1));
            await db.ProductionPlans.Where(x => x.Id == plans[2].Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => EF.Property<int>(x, "Status"), 3));
            await db.ProductionPlans.Where(x => x.Id == plans[3].Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(x => EF.Property<int>(x, "Status"), 4));
            db.ChangeTracker.Clear();

            var query = new ProductionModuleQueryService(db, clock);
            var visible = await query.PlansAsync(new ProductionPlanListQuery(), CancellationToken.None);
            Assert.Equal(2, visible.TotalCount);
            Assert.Equal(new[] { plans[0].Id, plans[1].Id }.Order(), visible.Items.Select(x => x.Id).Order());
            var service = ApplicationServiceFactory.Plans(db, clock);
            Assert.Equal(2, (await service.ListAsync(null, null, 1, 25, CancellationToken.None)).TotalCount);
            foreach (var index in new[] { 2, 3, 4, 5 })
                await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetAsync(plans[index].Id, CancellationToken.None));

            Assert.Equal(2, await db.ProductionOutputs.CountAsync());
            Assert.Equal(16m, await db.ProductionOutputs.SumAsync(x => x.TotalProducedQuantity));
            Assert.Equal(6, await db.ProductionPlans.CountAsync());
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
            SqlConnection.ClearAllPools();
        }
    }

    private static async Task<Seed> SeedAsync(OeeDbContext db)
    {
        // Tạo cây tổ chức, ca, SKU, reason và standard tối thiểu cho workflow.
        var enterprise = DomainTestData.Create<EnterpriseEntity>(("Code", "ENT-P"), ("Name", "Enterprise"));
        var site = DomainTestData.Create<SiteEntity>(("Enterprise", enterprise), ("Code", "SITE-P"), ("Name", "Site"), ("TimeZoneId", "UTC"));
        var area = DomainTestData.Create<AreaEntity>(("Site", site), ("Code", "PROC-P"), ("Name", "Area"));
        var group = DomainTestData.Create<WorkCenterEntity>(("Area", area), ("Code", "LINE-P"), ("Name", "Line"));
        var workUnit = DomainTestData.Create<WorkUnitEntity>(("WorkCenter", group), ("Code", "M-P01"), ("Name", "Machine"));
        var shift = DomainTestData.Create<ShiftEntity>(("Site", site), ("Code", "S1"), ("Name", "Shift 1"), ("StartTime", new TimeOnly(0, 0)), ("EndTime", new TimeOnly(23, 59)));
        var product = DomainTestData.Create<ProductEntity>(("Code", "SKU-P"), ("Name", "Product"), ("DefaultUom", "unit"));
        var unknown = DomainTestData.Create<DowntimeReasonEntity>(("Code", "DR-UNKNOWN"), ("Name", "Unknown"), ("Category", DowntimeCategoryEnum.Unknown), ("Kind", DowntimeKindEnum.Unknown));
        var breakdown = DomainTestData.Create<DowntimeReasonEntity>(("Code", "DR-BREAK"), ("Name", "Breakdown"), ("Category", DowntimeCategoryEnum.AvailabilityLoss), ("Kind", DowntimeKindEnum.Breakdown));
        var standard = DomainTestData.Create<WorkUnitStandardEntity>(("WorkUnit", workUnit), ("Product", product), ("StandardSpeedPerMinute", 10m), ("Uom", "unit"), ("EffectiveFrom", new DateOnly(2026, 1, 1)));
        db.AddRange(enterprise, site, area, group, workUnit, shift, product, unknown, breakdown, standard);
        await db.SaveChangesAsync();
        return new Seed(workUnit, shift, product, unknown, breakdown);
    }

    private sealed record Seed(WorkUnitEntity WorkUnit, ShiftEntity Shift, ProductEntity Product, DowntimeReasonEntity UnknownReason, DowntimeReasonEntity BreakdownReason);
    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow { get; } = new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero); }
    private sealed class TestActor : ICurrentActor { public string? UserId => Guid.Empty.ToString(); public string UserName => "production-test"; public string? IpAddress => "127.0.0.1"; }
}
