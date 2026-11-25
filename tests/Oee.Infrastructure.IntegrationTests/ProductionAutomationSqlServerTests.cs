using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Oee.Application.Common;
using Oee.Application.Production.Automation;
using Oee.Application.Security;
using Oee.Domain.Common;
using Oee.Domain.Entities.Organization;
using Oee.Domain.Entities.Production;
using Oee.Infrastructure.Persistence;
using Oee.Infrastructure.Production.Automation;
using Oee.Tests.Fixtures;

namespace Oee.Infrastructure.IntegrationTests;

public sealed class ProductionAutomationSqlServerTests
{
    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task DurableScheduleCreatesAuditedTomorrowPlansAndRealShiftPdfOnIsolatedSqlDatabase()
    {
        var baseConnection = Environment.GetEnvironmentVariable("OEE_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(baseConnection)) return;
        var connection = new SqlConnectionStringBuilder(baseConnection)
        { InitialCatalog = $"OeeDb_Automation_{Guid.NewGuid():N}" };
        Assert.StartsWith("OeeDb_Automation_", connection.InitialCatalog, StringComparison.Ordinal);
        var directory = Path.Combine(Path.GetTempPath(), "OeeAutomationSqlTests", Guid.NewGuid().ToString("N"));
        var options = new DbContextOptionsBuilder<OeeDbContext>().UseSqlServer(connection.ConnectionString).Options;
        await using var db = new OeeDbContext(options);
        try
        {
            await db.Database.MigrateAsync();
            var enterprise = EnterpriseEntity.Create("E", "Enterprise");
            var site = DomainTestData.Create<SiteEntity>(("Enterprise", enterprise), ("Code", "S"), ("Name", "Site"), ("TimeZoneId", "Asia/Ho_Chi_Minh"));
            var area = DomainTestData.Create<AreaEntity>(("Site", site), ("Code", "A"), ("Name", "Area"));
            var group = DomainTestData.Create<WorkCenterEntity>(("Area", area), ("Code", "W"), ("Name", "Group"));
            var unit = DomainTestData.Create<WorkUnitEntity>(("WorkCenter", group), ("Code", "M01"), ("Name", "Machine"));
            var product = ProductEntity.Create("SKU01", "Product", "PCS");
            var shifts = new[]
            {
                DomainTestData.Create<ShiftEntity>(("Site", site), ("Code", "S1"), ("Name", "Morning"), ("StartTime", new TimeOnly(6, 0)), ("EndTime", new TimeOnly(14, 0))),
                DomainTestData.Create<ShiftEntity>(("Site", site), ("Code", "S2"), ("Name", "Afternoon"), ("StartTime", new TimeOnly(14, 0)), ("EndTime", new TimeOnly(22, 0))),
                DomainTestData.Create<ShiftEntity>(("Site", site), ("Code", "S3"), ("Name", "Night"), ("StartTime", new TimeOnly(22, 0)), ("EndTime", new TimeOnly(6, 0)))
            };
            var sample = SampleProductionPlanEntity.Create("KHM-0001", "Daily");
            db.AddRange(enterprise, site, area, group, unit, product, sample);
            db.AddRange(shifts);
            db.AddRange(shifts.Select(shift => SampleProductionPlanItemEntity.Create(sample.Id, unit.Id, product.Id, shift.Id, 100)));
            await db.SaveChangesAsync();
            var completedMorning = ProductionPlanEntity.Create(unit.Id, product.Id, "EXISTING-MORNING",
                Local(27, 6, 0), Local(27, 14, 0), 1000, shifts[0].Id);
            db.Add(completedMorning);
            db.Add(ProductionOutputEntity.Create(completedMorning.Id, unit.Id, product.Id,
                Local(27, 6, 0), Local(27, 14, 0), 2.5m, 1000, DataSourceEnum.Manual, 20, shifts[0].Id));
            await db.SaveChangesAsync();

            var clock = new Clock { UtcNow = Local(27, 13, 0) };
            var checker = new Checker();
            var services = new ServiceCollection().AddLogging();
            services.AddOeeInfrastructure(connection.ConnectionString);
            services.AddSingleton<IClock>(clock);
            services.AddSingleton<ICapabilityChecker>(checker);
            services.Configure<PlanAutomationStorageOptions>(value => value.StoragePath = directory);
            await using var provider = services.BuildServiceProvider();
            async Task<T> Execute<T>(IRequest<T> request)
            {
                await using var scope = provider.CreateAsyncScope();
                return await scope.ServiceProvider.GetRequiredService<IUseCaseExecutor>().ExecuteAsync(request);
            }

            await Execute(new SaveProductionPlanScheduleCommand(sample.Id, new(16, 0), true, null, true));
            checker.Allowed = false; // Worker không giả lập một tài khoản Admin để ghi kế hoạch.
            Assert.Null(await Execute(new RunDueProductionPlanScheduleCommand()));
            clock.UtcNow = Local(27, 14, 5);
            Assert.Equal(1, await Execute(new RunDueShiftReportsCommand()));
            checker.Allowed = true;
            var state = await Execute(new GetProductionPlanScheduleQuery());
            var report = Assert.Single(state!.Reports!);
            Assert.Equal(shifts[0].Id, report.ShiftId);
            Assert.True(report.HasData);
            Assert.Equal(new DateOnly(2026, 10, 27), report.OperationalDate);
            var pdf = await Execute(new GetAutomaticShiftPdfQuery(report.Id));
            Assert.True(pdf.Content.Length > 1000);
            Assert.True(pdf.Content.AsSpan().StartsWith("%PDF-"u8));
            var evidence = Environment.GetEnvironmentVariable("OEE_AUTOMATION_PDF_EVIDENCE");
            if (!string.IsNullOrWhiteSpace(evidence)) await File.WriteAllBytesAsync(evidence, pdf.Content);

            checker.Allowed = false;
            clock.UtcNow = Local(27, 16, 0);
            var results = await Task.WhenAll(Execute(new RunDueProductionPlanScheduleCommand()), Execute(new RunDueProductionPlanScheduleCommand()));
            Assert.Single(results, result => result is not null);
            Assert.Equal(4, (await db.ProductionPlans.AsNoTracking().ToArrayAsync()).Length);
            var night = await db.ProductionPlans.AsNoTracking().SingleAsync(plan => plan.ShiftId == shifts[2].Id);
            Assert.Equal(Local(28, 22, 0), night.PlannedStart);
            Assert.Equal(Local(29, 6, 0), night.PlannedEnd);
            Assert.Equal(3, await db.ProductionPlans.CountAsync(plan => plan.SourceSampleProductionPlanId == sample.Id));
            Assert.True(await db.AuditLogs.AnyAsync(log => log.Action == "BulkProductionPlansCreated"));
            Assert.Null(await Execute(new RunDueProductionPlanScheduleCommand()));
            Assert.Equal(0, await Execute(new RunDueShiftReportsCommand()));

            // SQL commit trước khi lưu acknowledgement lịch: lần chạy lại phải skip đủ ba kế hoạch.
            await using (var session = await provider.GetRequiredService<IProductionPlanScheduleStore>().OpenAsync(default))
                await session.SaveAsync(session.Schedule! with { LastRunDate = null, LastAttemptAt = null }, default);
            var replay = await Execute(new RunDueProductionPlanScheduleCommand());
            Assert.Equal(0, replay!.Created);
            Assert.Equal(3, replay.Skipped);
            Assert.Equal(4, await db.ProductionPlans.CountAsync());
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
            var safeRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "OeeAutomationSqlTests")) + Path.DirectorySeparatorChar;
            Assert.StartsWith(safeRoot, Path.GetFullPath(directory), StringComparison.OrdinalIgnoreCase);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private static DateTimeOffset Local(int day, int hour, int minute) => new(2026, 10, day, hour, minute, 0, TimeSpan.FromHours(7));
    private sealed class Clock : IClock { public DateTimeOffset UtcNow { get; set; } }
    private sealed class Checker : ICapabilityChecker
    {
        public bool Allowed { get; set; } = true;
        public bool HasCapability(string capability) => Allowed;
    }
}
