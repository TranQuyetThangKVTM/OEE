using System.Data;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Contracts.Reporting;
using Oee.Application.Operations;
using Oee.Application.Production.Automation;
using Oee.Application.Production.Plans.Create;
using Oee.Application.Reporting;
using Oee.Application.Security;
using Oee.Domain.Entities.Organization;
using Oee.Domain.Entities.Production;
using Oee.Domain.Platform;
using Oee.Infrastructure.Production.Automation;
using Oee.Tests.Fixtures;

namespace Oee.Infrastructure.IntegrationTests;

public sealed class ProductionAutomationTests
{
    [Theory]
    [InlineData(BulkPlanCreationModeContract.Direct, "SKU01")]
    [InlineData(BulkPlanCreationModeContract.FromSample, "SKU02")]
    public async Task NewPlanCodesUseSkuAppliedDateShiftCodeAndMachineName(BulkPlanCreationModeContract mode, string productCode)
    {
        await using var fixture = new Fixture(productCode);
        fixture.Unit.Update("M01", "Blow Moulder", null);
        var request = new BulkProductionPlanRequest(mode, [fixture.Unit.Id], fixture.Shifts.Select(shift => shift.Id).ToArray(),
            fixture.Product.Id, fixture.Sample.Id, new DateOnly(2026, 11, 1), 100, null);

        var result = await fixture.Execute(new CreateProductionPlansCommand(request));

        Assert.Equal(3, result.CreatedCount);
        Assert.Equal(new[] { $"{productCode}-20261101-S01-BlowMoulder", $"{productCode}-20261101-S02-BlowMoulder", $"{productCode}-20261101-S03-BlowMoulder" },
            fixture.Plans.Select(plan => plan.Code).OrderBy(code => code).ToArray());
        var night = Assert.Single(fixture.Plans, plan => plan.ShiftId == fixture.Shifts[2].Id);
        Assert.Equal(Local(2026, 11, 2, 6, 0), night.PlannedEnd);
    }

    [Theory]
    [InlineData("Blow Moulder")]
    [InlineData("Blow Moulder Production Machine With A Long Name")]
    public async Task RecreatingDeletedPlansReservesOldCodesAndKeepsNewCodesWithinDatabaseLimit(string machineName)
    {
        await using var fixture = new Fixture();
        fixture.Unit.Update("M01", machineName, null);
        var request = new BulkProductionPlanRequest(BulkPlanCreationModeContract.FromSample, [], [], null,
            fixture.Sample.Id, new DateOnly(2026, 11, 1), null, null);
        await fixture.Execute(new CreateProductionPlansCommand(request));
        var original = fixture.Plans.Select(plan => plan.Code).ToArray();
        foreach (var plan in fixture.Plans)
            plan.Delete(fixture.Now);

        var recreated = await fixture.Execute(new CreateProductionPlansCommand(request));

        Assert.Equal(3, recreated.CreatedCount);
        Assert.Equal(original, fixture.Plans.Take(3).Select(plan => plan.Code).ToArray());
        Assert.Equal(6, fixture.Plans.Select(plan => plan.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.All(fixture.Plans.Skip(3), plan =>
        {
            Assert.EndsWith("-2", plan.Code, StringComparison.Ordinal);
            Assert.True(plan.Code.Length <= 32);
        });
    }

    [Fact]
    public async Task OversizedSkuRejectsTheBatchWithoutSavingPlans()
    {
        await using var fixture = new Fixture(new string('S', 32));
        var request = new BulkProductionPlanRequest(BulkPlanCreationModeContract.FromSample, [], [], null,
            fixture.Sample.Id, new DateOnly(2026, 11, 1), null, null);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Execute(new CreateProductionPlansCommand(request)));

        Assert.Contains("Mã SKU hoặc mã ca quá dài", error.Message, StringComparison.Ordinal);
        Assert.Empty(fixture.Plans);
    }

    [Fact]
    public async Task PermissionIsRequiredAndReadingUnconfiguredScheduleDoesNotCreateFiles()
    {
        await using var fixture = new Fixture();
        fixture.Allowed = false;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Execute(new SaveProductionPlanScheduleCommand(fixture.Sample.Id, new(16, 0), true, null)));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Execute(new GetProductionPlanScheduleQuery()));
        Assert.False(fixture.Store.Exists);
        fixture.Allowed = true;
        Assert.Null(await fixture.Execute(new GetProductionPlanScheduleQuery()));
        Assert.False(Directory.Exists(fixture.Path));
    }

    [Fact]
    public async Task CreatesTomorrowAtSiteTimeHandlesNightShiftAndSurvivesRestartWithoutDuplicates()
    {
        await using var fixture = new Fixture();
        fixture.Now = Local(2026, 12, 31, 15, 59);
        await fixture.Save();
        Assert.Null(await fixture.Execute(new RunDueProductionPlanScheduleCommand()));
        fixture.Now = Local(2026, 12, 31, 16, 0);
        var runs = await Task.WhenAll(fixture.Execute(new RunDueProductionPlanScheduleCommand()), fixture.Execute(new RunDueProductionPlanScheduleCommand()));
        Assert.Single(runs, result => result is not null);
        Assert.Equal(3, fixture.Plans.Count);
        var night = Assert.Single(fixture.Plans, plan => plan.ShiftId == fixture.Shifts[2].Id);
        Assert.Equal(Local(2027, 1, 1, 22, 0), night.PlannedStart);
        Assert.Equal(Local(2027, 1, 2, 6, 0), night.PlannedEnd);
        fixture.RestartStore();
        Assert.Null(await fixture.Execute(new RunDueProductionPlanScheduleCommand()));
        Assert.Equal(3, fixture.Plans.Count);
        // Mô phỏng SQL đã commit nhưng IIS dừng trước khi lưu kết quả lịch.
        await using (var session = await fixture.Store.OpenAsync(default))
            await session.SaveAsync(session.Schedule! with { LastRunDate = null, LastAttemptAt = null }, default);
        var retried = await fixture.Execute(new RunDueProductionPlanScheduleCommand());
        Assert.Equal(0, retried!.Created);
        Assert.Equal(3, retried.Skipped);
        Assert.Equal(3, fixture.Plans.Count);
    }

    [Fact]
    public async Task ExistingPlansAreSkippedAndOverlappingDifferentSkuIsNeverOverwritten()
    {
        await using var fixture = new Fixture();
        fixture.Now = Local(2026, 10, 27, 16, 0);
        await fixture.Save();
        fixture.Plans.Add(ProductionPlanEntity.Create(fixture.Unit.Id, fixture.Product.Id, "EXISTING",
            Local(2026, 10, 28, 6, 0), Local(2026, 10, 28, 14, 0), 99, fixture.Shifts[0].Id));
        fixture.Plans.Add(ProductionPlanEntity.Create(fixture.Unit.Id, Guid.NewGuid(), "OTHER-SKU",
            Local(2026, 10, 28, 14, 0), Local(2026, 10, 28, 22, 0), 77, fixture.Shifts[1].Id));
        var result = await fixture.Execute(new RunDueProductionPlanScheduleCommand());
        Assert.Equal(1, result!.Created);
        Assert.Equal(1, result.Skipped);
        Assert.Equal(1, result.Conflicts);
        Assert.Equal(99, fixture.Plans.Single(plan => plan.Code == "EXISTING").PlannedQuantity);
        Assert.Equal(77, fixture.Plans.Single(plan => plan.Code == "OTHER-SKU").PlannedQuantity);
    }

    [Fact]
    public async Task OptimisticRevisionPreventsLostUpdatesAndDeletedTemplateCanStillBeDisabled()
    {
        await using var fixture = new Fixture();
        var saved = await fixture.Save();
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Execute(new SaveProductionPlanScheduleCommand(fixture.Sample.Id, new(17, 0), true, null)));
        fixture.TemplateExists = false;
        var disabled = await fixture.Execute(new SaveProductionPlanScheduleCommand(fixture.Sample.Id, new(17, 0), false, saved.Revision));
        Assert.False(disabled.Enabled);
        Assert.False(disabled.PdfEnabled);
        fixture.Now = fixture.Now.AddDays(1);
        Assert.Null(await fixture.Execute(new RunDueProductionPlanScheduleCommand()));
        Assert.Equal(0, await fixture.Execute(new RunDueShiftReportsCommand()));
    }

    [Fact]
    public async Task FailureIsVisibleAndRetriesAfterFiveMinutes()
    {
        await using var fixture = new Fixture();
        fixture.Now = Local(2026, 10, 27, 16, 0);
        await fixture.Save();
        fixture.FailCreation = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Execute(new RunDueProductionPlanScheduleCommand()));
        Assert.NotNull((await fixture.Execute(new GetProductionPlanScheduleQuery()))!.LastError);
        fixture.FailCreation = false;
        fixture.Now = fixture.Now.AddMinutes(4);
        Assert.Null(await fixture.Execute(new RunDueProductionPlanScheduleCommand()));
        fixture.Now = fixture.Now.AddMinutes(1);
        Assert.Equal(3, (await fixture.Execute(new RunDueProductionPlanScheduleCommand()))!.Created);
        Assert.Null((await fixture.Execute(new GetProductionPlanScheduleQuery()))!.LastError);
    }

    [Fact]
    public async Task NightPdfWaitsForShiftEndUsesStartDateAndDoesNotRepeatAfterRestart()
    {
        await using var fixture = new Fixture();
        fixture.Now = Local(2026, 10, 27, 23, 0);
        await fixture.Save(planEnabled: false, pdfEnabled: true);
        Assert.Equal(0, await fixture.Execute(new RunDueShiftReportsCommand()));
        fixture.Now = Local(2026, 10, 28, 6, 4);
        Assert.Equal(0, await fixture.Execute(new RunDueShiftReportsCommand()));
        fixture.Now = fixture.Now.AddMinutes(1);
        Assert.Equal(1, await fixture.Execute(new RunDueShiftReportsCommand()));
        var query = Assert.Single(fixture.Queries);
        Assert.Equal(ReportKind.ShiftKpi, query.Kind);
        Assert.Equal(new DateOnly(2026, 10, 27), query.FromDate);
        Assert.Equal(query.FromDate, query.ToDate);
        Assert.Equal(fixture.Shifts[2].Id, query.ShiftId);
        Assert.Equal(fixture.Site.Id, query.SiteId);
        Assert.Empty(fixture.Plans);
        fixture.RestartStore();
        Assert.Equal(0, await fixture.Execute(new RunDueShiftReportsCommand()));
        var state = await fixture.Execute(new GetProductionPlanScheduleQuery());
        var report = Assert.Single(state!.Reports!);
        Assert.False(report.HasData); // Không có dữ liệu thì PDF phải nêu rõ; không bịa số liệu.
        var download = await fixture.Execute(new GetAutomaticShiftPdfQuery(report.Id));
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(download.Content), StringComparison.Ordinal);
        fixture.Allowed = false;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Execute(new GetAutomaticShiftPdfQuery(report.Id)));
        fixture.Allowed = true;
        await Assert.ThrowsAsync<KeyNotFoundException>(() => fixture.Execute(new GetAutomaticShiftPdfQuery(Guid.NewGuid())));
    }

    [Fact]
    public async Task PdfFailureIsRetriedWithoutDuplicateFilesOrPreventingPlanGeneration()
    {
        await using var fixture = new Fixture();
        fixture.Now = Local(2026, 10, 27, 13, 0);
        await fixture.Save(pdfEnabled: true);
        fixture.Now = Local(2026, 10, 27, 14, 5);
        fixture.Exporter.Fail = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Execute(new RunDueShiftReportsCommand()));
        Assert.NotNull((await fixture.Execute(new GetProductionPlanScheduleQuery()))!.PdfLastError);
        fixture.Exporter.Fail = false;
        Assert.Equal(0, await fixture.Execute(new RunDueShiftReportsCommand()));
        fixture.Now = fixture.Now.AddMinutes(5);
        Assert.Equal(1, await fixture.Execute(new RunDueShiftReportsCommand()));
        Assert.Equal(0, await fixture.Execute(new RunDueShiftReportsCommand()));
        fixture.Now = Local(2026, 10, 27, 16, 0);
        Assert.Equal(3, (await fixture.Execute(new RunDueProductionPlanScheduleCommand()))!.Created);
        Assert.Single(Directory.GetFiles(System.IO.Path.Combine(fixture.Path, "Reports"), "*.pdf"));
    }

    [Fact]
    public async Task SharedFileLockRespectsCancellationAndMalformedStateFailsClosed()
    {
        await using var fixture = new Fixture();
        await fixture.Save();
        await using (var lease = await fixture.Store.OpenAsync(default))
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Store.OpenAsync(cts.Token));
        }
        await File.WriteAllTextAsync(System.IO.Path.Combine(fixture.Path, "schedule.json"), "{broken");
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => fixture.Execute(new RunDueProductionPlanScheduleCommand()));
        Assert.Empty(fixture.Plans);
    }

    private static DateTimeOffset Local(int year, int month, int day, int hour, int minute) => new(year, month, day, hour, minute, 0, TimeSpan.FromHours(7));

    private sealed class Fixture : IAsyncDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "OeeAutomationTests", Guid.NewGuid().ToString("N"));
        public DateTimeOffset Now { get; set; } = Local(2026, 10, 27, 15, 0);
        public bool Allowed { get; set; } = true;
        public bool TemplateExists { get; set; } = true;
        public bool FailCreation { get; set; }
        public SiteEntity Site { get; }
        public WorkUnitEntity Unit { get; }
        public ProductEntity Product { get; }
        public ShiftEntity[] Shifts { get; }
        public SampleProductionPlanEntity Sample { get; } = SampleProductionPlanEntity.Create("KHM-0001", "Daily");
        public List<ProductionPlanEntity> Plans { get; } = [];
        public List<ReportQuery> Queries { get; } = [];
        public Exporter Exporter { get; } = new();
        public JsonProductionPlanScheduleStore Store { get; private set; } = null!;
        private readonly ServiceProvider _provider;

        public Fixture(string productCode = "SKU")
        {
            Product = ProductEntity.Create(productCode, "Product", "PCS");
            var enterprise = EnterpriseEntity.Create("E", "Enterprise");
            Site = DomainTestData.Create<SiteEntity>(("Enterprise", enterprise), ("EnterpriseId", enterprise.Id), ("Code", "S"), ("Name", "Site"), ("TimeZoneId", "Asia/Ho_Chi_Minh"));
            var area = DomainTestData.Create<AreaEntity>(("Site", Site), ("SiteId", Site.Id), ("Code", "A"), ("Name", "Area"));
            var group = DomainTestData.Create<WorkCenterEntity>(("Area", area), ("AreaId", area.Id), ("Code", "W"), ("Name", "Group"));
            Unit = DomainTestData.Create<WorkUnitEntity>(("WorkCenter", group), ("WorkCenterId", group.Id), ("Code", "M01"), ("Name", "Machine"));
            Shifts = [Shift("S01", 6, 14), Shift("S02", 14, 22), Shift("S03", 22, 6)];
            var items = Shifts.Select(shift => DomainTestData.Create<SampleProductionPlanItemEntity>(
                ("SampleProductionPlan", Sample), ("SampleProductionPlanId", Sample.Id), ("WorkUnit", Unit), ("WorkUnitId", Unit.Id),
                ("Product", Product), ("ProductId", Product.Id), ("Shift", shift), ("ShiftId", shift.Id), ("PlannedQuantity", 100m))).ToArray();
            RestartStore();
            var services = new ServiceCollection().AddLogging().AddOeeInfrastructure("", includeIdentityStore: false);
            services.AddSingleton<IClock>(new Clock(this));
            services.AddScoped<IProductionPlanScheduleStore>(_ => Store);
            services.Configure<PlanAutomationStorageOptions>(options => options.StoragePath = Path);
            services.AddScoped<ICapabilityChecker>(_ => new Checker(this));
            services.AddScoped<IProductionPlanStore>(_ => Stub<IProductionPlanStore>((name, args) => name switch
            {
                "FindSampleAsync" => Task.FromResult<SampleProductionPlanEntity?>(TemplateExists ? Sample : null),
                "ReadSampleItemsAsync" => Task.FromResult(items),
                "ReadOverlappingPlansAsync" => Task.FromResult(Plans.Where(plan => !plan.DeletedAt.HasValue).ToArray()),
                "ReadReservedCodesAsync" => Task.FromResult(Plans.Where(plan => plan.Code.StartsWith((string)args[0]!, StringComparison.OrdinalIgnoreCase)).Select(plan => plan.Code).ToArray()),
                "ReadActiveUnitsAsync" => Task.FromResult(new[] { Unit }),
                "ReadActiveShiftsAsync" => Task.FromResult(Shifts),
                "FindActiveProductAsync" => Task.FromResult<ProductEntity?>(Product),
                "AddRange" => Add((IEnumerable<ProductionPlanEntity>)args[0]!),
                _ => throw new InvalidOperationException(name)
            }));
            services.AddScoped<IApplicationUnitOfWork>(_ => new UnitOfWork());
            services.AddScoped<IReportAnalysisQueryService>(_ => Stub<IReportAnalysisQueryService>((name, args) =>
            {
                if (name != "QueryAsync") throw new InvalidOperationException(name);
                var query = (ReportQuery)args[0]!;
                Queries.Add(query);
                return Task.FromResult(new ReportData(query.Kind, "OEE", "Test", Site.TimeZoneId, Now, [], [], [], [], 0, false));
            }));
            services.AddSingleton<IReportExportService>(Exporter);
            _provider = services.BuildServiceProvider();
        }

        private object? Add(IEnumerable<ProductionPlanEntity> plans)
        {
            if (FailCreation) throw new InvalidOperationException("Injected SQL failure");
            Plans.AddRange(plans);
            return null;
        }

        private ShiftEntity Shift(string code, int start, int end) => DomainTestData.Create<ShiftEntity>(
            ("Site", Site), ("SiteId", Site.Id), ("Code", code), ("Name", code), ("StartTime", new TimeOnly(start, 0)), ("EndTime", new TimeOnly(end, 0)));

        public void RestartStore() => Store = new JsonProductionPlanScheduleStore(Options.Create(new PlanAutomationStorageOptions { StoragePath = Path }));
        public Task<ProductionPlanSchedule> Save(bool planEnabled = true, bool pdfEnabled = false) => Execute(
            new SaveProductionPlanScheduleCommand(Sample.Id, new(16, 0), planEnabled, null, pdfEnabled));
        public async Task<T> Execute<T>(IRequest<T> request)
        {
            await using var scope = _provider.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<IUseCaseExecutor>().ExecuteAsync(request);
        }
        public async ValueTask DisposeAsync()
        {
            await _provider.DisposeAsync();
            // Thư mục test là Guid tạo trực tiếp dưới temp, không nhận đường dẫn từ input bên ngoài.
            Assert.StartsWith(System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "OeeAutomationTests"))
                + System.IO.Path.DirectorySeparatorChar, System.IO.Path.GetFullPath(Path), StringComparison.OrdinalIgnoreCase);
            if (Directory.Exists(Path)) Directory.Delete(Path, true);
        }
    }

    private sealed class Clock(Fixture fixture) : IClock { public DateTimeOffset UtcNow => fixture.Now.ToUniversalTime(); }
    private sealed class Checker(Fixture fixture) : ICapabilityChecker { public bool HasCapability(string capability) => fixture.Allowed; }
    private sealed class UnitOfWork : IApplicationUnitOfWork
    {
        public bool HasChanges => false;
        public Task<IApplicationTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken cancellationToken) => Task.FromResult<IApplicationTransaction>(new Transaction());
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
        public void ExpectRowVersion(global::Oee.Domain.Common.SoftDeletableAuditedEntity entity, byte[] expected) { }
        public void ExpectUpdatedAt(global::Oee.Domain.Common.AuditedEntity entity, DateTimeOffset? expected) { }
        public void RequireUpdatedAtWrite(global::Oee.Domain.Common.AuditedEntity entity) { }
        public void AddAudit(AuditLogEntity entry) { }
        public void DiscardChanges() { }
    }
    private sealed class Transaction : IApplicationTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RollbackAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    public sealed class Exporter : IReportExportService
    {
        public bool Fail { get; set; }
        public byte[] RenderPdf(ReportData data) => Fail ? throw new InvalidOperationException("Injected render failure") : "%PDF-1.7 test"u8.ToArray();
    }
    private static T Stub<T>(Func<string, object?[], object?> call) where T : class
    {
        var proxy = DispatchProxy.Create<T, PortProxy>();
        ((PortProxy)(object)proxy).Call = call;
        return proxy;
    }
    public class PortProxy : DispatchProxy
    {
        public Func<string, object?[], object?> Call { get; set; } = default!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Call(targetMethod!.Name, args ?? []);
    }
}
