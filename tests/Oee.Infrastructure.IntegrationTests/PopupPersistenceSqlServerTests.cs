using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Security;
using Oee.Domain.Entities.Organization;
using Oee.Infrastructure.Audit;
using Oee.Infrastructure.Persistence;
using Oee.Tests.Fixtures;

namespace Oee.Infrastructure.IntegrationTests;

/// <summary>
/// Integration Test SQL Server cho popup bulk kế hoạch mẫu và sinh kế hoạch sản xuất từ mẫu.
/// </summary>
/// <remarks>
/// Test đặc biệt kiểm tra transaction rollback, unique index khi request đồng thời, traceability và ca qua nửa đêm.
/// </remarks>
public sealed class PopupPersistenceSqlServerTests
{
    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task SampleAndProductionBulkPopupsPersistAtomicallyWithTraceabilityAndDuplicateProtection()
    {
        // Arrange: tạo database tách biệt và seed 2 thiết bị × 3 ca cùng hai SKU.
        var baseConnectionString = Environment.GetEnvironmentVariable("OEE_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(baseConnectionString)) return;

        var databaseName = $"OeeDb_PopupTests_{Guid.NewGuid():N}";
        var connection = new SqlConnectionStringBuilder(baseConnectionString) { InitialCatalog = databaseName };
        Assert.StartsWith("OeeDb_PopupTests_", connection.InitialCatalog, StringComparison.Ordinal);
        var options = Options(connection.ConnectionString);
        await using var db = new OeeDbContext(options);
        try
        {
            await db.Database.MigrateAsync();
            var seed = await SeedAsync(db);
            var clock = new FixedClock();
            var samples = ApplicationServiceFactory.Samples(db, clock, new TestActor());
            var plans = ApplicationServiceFactory.Plans(db, clock, new TestActor());

            // Act & Assert: server-side validation chặn tên rỗng/sản lượng bằng 0 trước khi ghi cha hoặc dòng.
            await Assert.ThrowsAsync<ArgumentException>(() => samples.CreateWithItemsAsync(
                new SamplePlanBulkCreateRequest(" ", null, seed.WorkUnits.Select(x => x.Id).ToArray(),
                    seed.Shifts.Select(x => x.Id).ToArray(), seed.Product.Id, 1000, null), CancellationToken.None));
            await Assert.ThrowsAsync<ArgumentException>(() => samples.CreateWithItemsAsync(
                new SamplePlanBulkCreateRequest("Zero", null, seed.WorkUnits.Select(x => x.Id).ToArray(),
                    seed.Shifts.Select(x => x.Id).ToArray(), seed.Product.Id, 0, null), CancellationToken.None));
            Assert.Empty(await db.SampleProductionPlans.AsNoTracking().ToArrayAsync());

            // Act & Assert: lỗi được chèn sau SaveChanges làm transaction rollback cả cha lẫn dòng.
            await using (var failingDb = new OeeDbContext(Options(connection.ConnectionString, failAfterSave: true)))
            {
                var failingService = ApplicationServiceFactory.Samples(failingDb, clock, new TestActor());
                await Assert.ThrowsAsync<InvalidOperationException>(() => failingService.CreateWithItemsAsync(
                    new SamplePlanBulkCreateRequest("Rollback sample", null, [seed.WorkUnits[0].Id],
                        [seed.Shifts[0].Id], seed.Product.Id, 100, null), CancellationToken.None));
            }
            db.ChangeTracker.Clear();
            Assert.Empty(await db.SampleProductionPlans.AsNoTracking().ToArrayAsync());
            Assert.Empty(await db.SampleProductionPlanItems.AsNoTracking().ToArrayAsync());

            // Act & Assert: tích Descartes 2 thiết bị × 3 ca tạo đúng sáu dòng trong một mẫu.
            var created = await samples.CreateWithItemsAsync(new SamplePlanBulkCreateRequest(
                "Mẫu đóng gói 2x3",
                "atomic template",
                seed.WorkUnits.Select(x => x.Id).ToArray(),
                seed.Shifts.Select(x => x.Id).ToArray(),
                seed.Product.Id,
                1000,
                "popup"), CancellationToken.None);
            Assert.Equal(6, created.RequestedCount);
            Assert.Equal(6, created.CreatedCount);
            Assert.Equal(6, await db.SampleProductionPlanItems.CountAsync(x => x.SampleProductionPlanId == created.SamplePlanId));

            // Act & Assert: preview và command cùng nhận diện sáu tổ hợp trùng, không tạo bản ghi thứ hai.
            var duplicatePreview = await samples.PreviewAddItemsAsync(created.SamplePlanId,
                new SamplePlanBulkAddItemsRequest(seed.WorkUnits.Select(x => x.Id).ToArray(),
                    seed.Shifts.Select(x => x.Id).ToArray(), seed.Product.Id, 2000, null), CancellationToken.None);
            Assert.Equal(6, duplicatePreview.DuplicateCount);
            var duplicateResult = await samples.AddItemsAsync(created.SamplePlanId,
                new SamplePlanBulkAddItemsRequest(seed.WorkUnits.Select(x => x.Id).ToArray(),
                    seed.Shifts.Select(x => x.Id).ToArray(), seed.Product.Id, 2000, null), CancellationToken.None);
            Assert.Equal(0, duplicateResult.CreatedCount);
            Assert.Equal(6, duplicateResult.SkippedCount);
            Assert.Equal(6, await db.SampleProductionPlanItems.CountAsync(x => x.SampleProductionPlanId == created.SamplePlanId));

            // Arrange & Act: hai DbContext mô phỏng hai người lưu cùng tổ hợp đồng thời.
            var concurrentSample = await samples.CreateAsync(new SamplePlanRequest("CONCURRENT", "Concurrent", null), CancellationToken.None);
            async Task<bool> AddConcurrentlyAsync()
            {
                await using var isolated = new OeeDbContext(options);
                var service = ApplicationServiceFactory.Samples(isolated, clock, new TestActor());
                try
                {
                    await service.AddItemsAsync(concurrentSample.Id,
                        new SamplePlanBulkAddItemsRequest([seed.WorkUnits[0].Id], [seed.Shifts[0].Id],
                            seed.SecondProduct.Id, 500, "concurrent"), CancellationToken.None);
                    return true;
                }
                catch (Exception exception) when (exception is InvalidOperationException or SqlException)
                {
                    return false;
                }
            }
            var concurrentResults = await Task.WhenAll(AddConcurrentlyAsync(), AddConcurrentlyAsync());
            Assert.Contains(true, concurrentResults);
            db.ChangeTracker.Clear();
            Assert.Equal(1, await db.SampleProductionPlanItems.CountAsync(x => x.SampleProductionPlanId == concurrentSample.Id
                && x.WorkUnitId == seed.WorkUnits[0].Id && x.ShiftId == seed.Shifts[0].Id && x.ProductId == seed.SecondProduct.Id));

            // Arrange & Act: chụp mẫu trước khi sinh ProductionPlan để chứng minh nguồn không bị chỉnh sửa.
            var beforeSnapshot = await db.SampleProductionPlanItems.AsNoTracking()
                .Where(x => x.SampleProductionPlanId == created.SamplePlanId)
                .OrderBy(x => x.Id).Select(x => new { x.Id, x.PlannedQuantity, x.Notes, x.WorkUnitId, x.ProductId, x.ShiftId }).ToArrayAsync();
            var request = new BulkProductionPlanRequest(BulkPlanCreationModeContract.FromSample, [], [], null,
                created.SamplePlanId, new DateOnly(2026, 8, 12), null, "from popup");
            var preview = await plans.PreviewBulkAsync(request, CancellationToken.None);
            Assert.Equal(6, preview.RequestedCount);
            Assert.Equal(6, preview.ValidCount);
            var generated = await plans.CreateBulkAsync(request, CancellationToken.None);
            Assert.Equal(6, generated.CreatedCount);
            var generatedRows = await db.ProductionPlans.AsNoTracking()
                .Where(x => generated.CreatedPlanIds.Contains(x.Id)).ToArrayAsync();
            // Assert: mỗi kế hoạch giữ FK truy vết về mẫu cha và dòng nguồn riêng biệt.
            Assert.All(generatedRows, row =>
            {
                Assert.Null(row.DeletedAt);
                Assert.StartsWith("SKU-POP-1-20260812-", row.Code, StringComparison.Ordinal);
                Assert.Equal(created.SamplePlanId, row.SourceSampleProductionPlanId);
                Assert.NotNull(row.SourceSampleProductionPlanItemId);
            });
            Assert.Equal(6, generatedRows.Select(x => x.SourceSampleProductionPlanItemId).Distinct().Count());

            var afterSnapshot = await db.SampleProductionPlanItems.AsNoTracking()
                .Where(x => x.SampleProductionPlanId == created.SamplePlanId)
                .OrderBy(x => x.Id).Select(x => new { x.Id, x.PlannedQuantity, x.Notes, x.WorkUnitId, x.ProductId, x.ShiftId }).ToArrayAsync();
            Assert.Equal(beforeSnapshot, afterSnapshot);

            // Assert: ca S3 dùng timezone Site và kết thúc vào ngày hôm sau, kéo dài đúng tám giờ.
            var zone = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            var nightPlan = generatedRows.Single(x => x.WorkUnitId == seed.WorkUnits[0].Id && x.ShiftId == seed.Shifts[2].Id);
            Assert.Equal(new DateTime(2026, 8, 12, 22, 0, 0), TimeZoneInfo.ConvertTime(nightPlan.PlannedStart, zone).DateTime);
            Assert.Equal(new DateTime(2026, 8, 13, 6, 0, 0), TimeZoneInfo.ConvertTime(nightPlan.PlannedEnd, zone).DateTime);
            Assert.Equal(TimeSpan.FromHours(8), nightPlan.PlannedEnd - nightPlan.PlannedStart);

            // Act & Assert: chạy lại cùng request có tính idempotent và SKU khác cùng slot được báo Conflict.
            var rerun = await plans.CreateBulkAsync(request, CancellationToken.None);
            Assert.Equal(0, rerun.CreatedCount);
            Assert.Equal(6, rerun.SkippedCount);
            Assert.Equal(6, await db.ProductionPlans.CountAsync(x => x.SourceSampleProductionPlanId == created.SamplePlanId));

            var conflict = await plans.CreateBulkAsync(new BulkProductionPlanRequest(
                BulkPlanCreationModeContract.Direct,
                [seed.WorkUnits[0].Id],
                [seed.Shifts[0].Id],
                seed.SecondProduct.Id,
                null,
                new DateOnly(2026, 8, 12),
                800,
                "different SKU"), CancellationToken.None);
            Assert.Equal(0, conflict.CreatedCount);
            Assert.Equal(1, conflict.ConflictCount);
            Assert.Contains(conflict.Combinations, x => x.State == "Conflict" && x.Reason!.Contains("không ghi đè", StringComparison.Ordinal));

            // Assert: audit bulk lưu số lượng tạo, bỏ qua/xung đột mà không ghi dữ liệu nhạy cảm.
            var bulkAudits = await db.AuditLogs.AsNoTracking()
                .Where(x => x.Action == "SampleProductionPlanCreated" || x.Action == "BulkProductionPlansCreated")
                .ToArrayAsync();
            Assert.Contains(bulkAudits, x => x.Changes!.Contains("\"CreatedCount\":6", StringComparison.Ordinal));
            await plans.DeleteAsync(nightPlan.Id, nightPlan.RowVersion, CancellationToken.None);
            var recreated = await plans.CreateBulkAsync(request, CancellationToken.None);
            Assert.Equal(1, recreated.CreatedCount);
            Assert.Equal(5, recreated.SkippedCount);
            var newCode = await db.ProductionPlans.Where(x => recreated.CreatedPlanIds.Contains(x.Id)).Select(x => x.Code).SingleAsync();
            Assert.NotEqual(nightPlan.Code, newCode);
            var copied = await samples.CopyAsync(created.SamplePlanId, "COPY-TEMPLATE", "Bản sao", CancellationToken.None);
            Assert.NotEqual(created.SamplePlanId, copied.Id);
            var copiedRows = await db.SampleProductionPlanItems.AsNoTracking().Where(x => x.SampleProductionPlanId == copied.Id).ToArrayAsync();
            Assert.Equal(6, copiedRows.Length);
            Assert.All(copiedRows, row => Assert.Contains(beforeSnapshot, original =>
                original.PlannedQuantity == row.PlannedQuantity && original.Notes == row.Notes
                && original.WorkUnitId == row.WorkUnitId && original.ProductId == row.ProductId && original.ShiftId == row.ShiftId));
            Assert.DoesNotContain(copiedRows, row => beforeSnapshot.Any(original => original.Id == row.Id));
            var copiedToken = Convert.FromBase64String(copied.RowVersion);
            await samples.DeleteAsync(copied.Id, copiedToken, CancellationToken.None);
            Assert.Empty(await db.SampleProductionPlanItems.Where(x => x.SampleProductionPlanId == copied.Id).ToArrayAsync());
            Assert.Equal(6, await db.SampleProductionPlanItems.CountAsync(x => x.SampleProductionPlanId == created.SamplePlanId));
        }
        finally
        {
            // Cleanup database test tách biệt.
            await db.Database.EnsureDeletedAsync();
            SqlConnection.ClearAllPools();
        }
    }

    private static DbContextOptions<OeeDbContext> Options(string connectionString, bool failAfterSave = false)
    {
        // Cùng cấu hình SQL/audit; interceptor tùy chọn mô phỏng lỗi trước khi transaction commit.
        var builder = new DbContextOptionsBuilder<OeeDbContext>()
            .UseSqlServer(connectionString)
            .AddInterceptors(new AuditSaveChangesInterceptor(new TestActor()));
        if (failAfterSave) builder.AddInterceptors(new FailAfterSaveInterceptor());
        return builder.Options;
    }

    private static async Task<Seed> SeedAsync(OeeDbContext db)
    {
        // Seed toàn bộ lookup một lần để tránh N+1 và giữ dữ liệu Arrange nhất quán.
        var enterprise = DomainTestData.Create<EnterpriseEntity>(("Code", "ENT-POP"), ("Name", "Enterprise"));
        var site = DomainTestData.Create<SiteEntity>(("Enterprise", enterprise), ("Code", "SITE-POP"), ("Name", "Site"), ("TimeZoneId", "SE Asia Standard Time"));
        var area = DomainTestData.Create<AreaEntity>(("Site", site), ("Code", "PROC-POP"), ("Name", "Area"));
        var group = DomainTestData.Create<WorkCenterEntity>(("Area", area), ("Code", "LINE-POP"), ("Name", "Line"));
        var units = new[]
        {
            DomainTestData.Create<WorkUnitEntity>(("WorkCenter", group), ("Code", "M-POP-1"), ("Name", "Machine 1")),
            DomainTestData.Create<WorkUnitEntity>(("WorkCenter", group), ("Code", "M-POP-2"), ("Name", "Machine 2")),
        };
        var shifts = new[]
        {
            DomainTestData.Create<ShiftEntity>(("Site", site), ("Code", "S1"), ("Name", "Morning"), ("StartTime", new TimeOnly(6, 0)), ("EndTime", new TimeOnly(14, 0))),
            DomainTestData.Create<ShiftEntity>(("Site", site), ("Code", "S2"), ("Name", "Afternoon"), ("StartTime", new TimeOnly(14, 0)), ("EndTime", new TimeOnly(22, 0))),
            DomainTestData.Create<ShiftEntity>(("Site", site), ("Code", "S3"), ("Name", "Night"), ("StartTime", new TimeOnly(22, 0)), ("EndTime", new TimeOnly(6, 0))),
        };
        var product = DomainTestData.Create<ProductEntity>(("Code", "SKU-POP-1"), ("Name", "Product 1"), ("DefaultUom", "PCS"));
        var secondProduct = DomainTestData.Create<ProductEntity>(("Code", "SKU-POP-2"), ("Name", "Product 2"), ("DefaultUom", "PCS"));
        db.AddRange(enterprise, site, area, group, product, secondProduct);
        db.AddRange(units);
        db.AddRange(shifts);
        await db.SaveChangesAsync();
        return new Seed(units, shifts, product, secondProduct);
    }

    private sealed record Seed(WorkUnitEntity[] WorkUnits, ShiftEntity[] Shifts, ProductEntity Product, ProductEntity SecondProduct);
    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow { get; } = new(2026, 8, 12, 12, 0, 0, TimeSpan.Zero); }
    private sealed class TestActor : ICurrentActor { public string? UserId => Guid.Empty.ToString(); public string UserName => "popup-test"; public string? IpAddress => "127.0.0.1"; }
    private sealed class FailAfterSaveInterceptor : SaveChangesInterceptor
    {
        // Ném lỗi có chủ đích sau SaveChanges để xác nhận transaction của service thực sự rollback.
        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<int>(new InvalidOperationException("Injected failure before transaction commit."));
    }
}
