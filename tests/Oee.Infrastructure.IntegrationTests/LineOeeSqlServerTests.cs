using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Oee.Application.Common;
using Oee.Application.Contracts.Modules;
using Oee.Application.Production.LineOff;
using Oee.Domain.Common;
using Oee.Domain.Downtime;
using Oee.Domain.Entities.Organization;
using Oee.Domain.Entities.Production;
using Oee.Domain.PlcIntegration;
using Oee.Infrastructure.Operations;
using Oee.Infrastructure.Persistence;
using Oee.Tests.Fixtures;

namespace Oee.Infrastructure.IntegrationTests;

public sealed class LineOeeSqlServerTests
{
    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task FillerAloneRepresentsLineAndMonthUsesWeightedTotalsWithLocalDayBoundaries()
    {
        await using var fixture = await Fixture.CreateAsync();
        if (fixture is null) return;
        var db = fixture.Db;
        var start = fixture.Start;
        var firstStart = start.AddDays(1).AddMinutes(-100);
        var firstEnd = start.AddDays(1);
        var secondEnd = firstEnd.AddMinutes(900);
        var firstPlan = fixture.Plan("P1", firstStart, firstEnd);
        var secondPlan = fixture.Plan("P2", firstEnd, secondEnd);
        var previousPlan = fixture.Plan("OLD", start.AddDays(-1), start);
        var other = DomainTestData.Create<WorkUnitEntity>(("WorkCenterId", fixture.Line.Id), ("Code", "B01"), ("Name", "BlowMoulder"));
        db.Add(other);
        db.AddRange(fixture.Output(firstPlan, firstStart, firstEnd, 50), fixture.Output(secondPlan, firstEnd, secondEnd, 900),
            fixture.Output(previousPlan, start.AddDays(-1), start, 5000));
        await db.SaveChangesAsync();
        var service = new DashboardQueryService(db, fixture.Clock, fixture.Calendar);
        var request = fixture.Request(new(2026, 11, 2)) with { WorkUnitId = other.Id, ProductId = Guid.NewGuid(), ShiftId = Guid.NewGuid() };
        var result = await service.GetLineOeeAsync(request, default);
        Assert.Null(result.Message);
        Assert.Equal("F01 · Filler", result.Machine);
        Assert.Equal(1m, result.Day!.Oee);
        Assert.Equal(900m, result.Day.GoodQuantity);
        Assert.Equal(.95m, result.Month!.Oee);
        Assert.Equal(1000m, result.Month.LoadingMinutes);
        Assert.Equal(950m, result.Month.GoodQuantity);
        var previousDay = await service.GetLineOeeAsync(request with { ToDate = new(2026, 11, 1) }, default);
        Assert.Equal(.5m, previousDay.Day!.Oee);
        Assert.Equal(50m, previousDay.Day.GoodQuantity); // Midnight belongs to previous day.
        var future = await service.GetLineOeeAsync(request with { ToDate = new(2026, 11, 4) }, default);
        Assert.Null(future.Day);
        Assert.NotNull(future.Message);
        var noPlan = await service.GetLineOeeAsync(request with { ToDate = new(2026, 11, 3) }, default);
        Assert.Null(noPlan.Day!.Oee);
        Assert.Equal(.95m, noPlan.Month!.Oee);
        var secondLine = DomainTestData.Create<WorkCenterEntity>(("AreaId", fixture.Line.AreaId), ("Code", "L2"), ("Name", "Line 2"));
        db.Add(DomainTestData.Create<WorkUnitEntity>(("WorkCenter", secondLine), ("Code", "F02"), ("Name", "Filler")));
        await db.SaveChangesAsync();
        var ambiguous = await service.GetLineOeeAsync(request with { WorkCenterId = null }, default);
        Assert.Null(ambiguous.Day);
        Assert.Contains("Có nhiều máy Filler", ambiguous.Message, StringComparison.Ordinal);
    }

    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task PartialOffKeepsProductionAndStopsButMissingOpcNeverBecomesOff()
    {
        await using var fixture = await Fixture.CreateAsync();
        if (fixture is null) return;
        var db = fixture.Db;
        var start = fixture.Start.AddHours(6);
        var middle = start.AddHours(4);
        var end = start.AddHours(8);
        var plan = fixture.Plan("HALF", start, end);
        db.Add(fixture.Output(plan, start, middle, 240));
        var reason = DomainTestData.Create<DowntimeReasonEntity>(("Code", "FAIL"), ("Name", "Breakdown"),
            ("Category", DowntimeCategoryEnum.AvailabilityLoss), ("Kind", DowntimeKindEnum.Breakdown));
        db.Add(DomainTestData.Create<DowntimeEventEntity>(("WorkUnitId", fixture.Filler.Id), ("DowntimeReason", reason),
            ("StartAt", middle), ("EndAt", end), ("Category", DowntimeCategoryEnum.AvailabilityLoss), ("Kind", DowntimeKindEnum.Breakdown), ("Source", DataSourceEnum.Manual)));
        await db.SaveChangesAsync();
        var service = new DashboardQueryService(db, fixture.Clock, fixture.Calendar);
        var request = fixture.Request(new(2026, 11, 1));
        var stopped = await service.GetLineOeeAsync(request, default);
        Assert.Equal(.5m, stopped.Day!.Oee);
        Assert.Equal(480m, stopped.Day.LoadingMinutes);

        fixture.Calendar.Set(fixture.Line.Id, middle, end);
        var halfOff = await service.GetLineOeeAsync(request, default);
        Assert.Equal(1m, halfOff.Day!.Oee);
        Assert.Equal(240m, halfOff.Day.LoadingMinutes);
        Assert.Equal(240m, halfOff.DayOffMinutes);
        Assert.Equal(1m, halfOff.Month!.Oee);

        var secondStart = start.AddDays(1);
        fixture.Plan("STOPPED", secondStart, secondStart.AddHours(8));
        db.Add(DomainTestData.Create<DowntimeEventEntity>(("WorkUnitId", fixture.Filler.Id), ("DowntimeReason", reason),
            ("StartAt", secondStart), ("EndAt", secondStart.AddHours(8)), ("Category", DowntimeCategoryEnum.AvailabilityLoss), ("Kind", DowntimeKindEnum.Breakdown), ("Source", DataSourceEnum.Manual)));
        await db.SaveChangesAsync();
        var second = await service.GetLineOeeAsync(fixture.Request(new(2026, 11, 2)), default);
        Assert.Equal(0m, second.Day!.Oee);
        Assert.Equal(480m, second.Day.LoadingMinutes);
        Assert.Equal(240m / 720m, second.Month!.Oee);

        var connection = DomainTestData.Create<OpcUaConnectionEntity>(("Code", "OPC"), ("Name", "OPC"), ("EndpointUrl", "opc.tcp://fake:4840"), ("IsActive", true));
        db.Add(DomainTestData.Create<PlcDeviceMappingEntity>(("OpcUaConnection", connection), ("WorkUnitId", fixture.Filler.Id), ("ExternalDeviceId", "F01"), ("IsActive", true)));
        db.AddRange(PlcSessionEventEntity.Create(connection.Id, secondStart.AddHours(-1), ConnectivityStateEnum.Connected, "Connected"),
            PlcSessionEventEntity.Create(connection.Id, secondStart.AddHours(2), ConnectivityStateEnum.Disconnected, "Disconnected"),
            PlcSessionEventEntity.Create(connection.Id, secondStart.AddHours(3), ConnectivityStateEnum.Connected, "Connected"));
        await db.SaveChangesAsync();
        var incomplete = await service.GetLineOeeAsync(fixture.Request(new(2026, 11, 2)), default);
        Assert.Null(incomplete.Day);
        Assert.Null(incomplete.Month);
        Assert.Equal(60m, incomplete.DayMissingMinutes); // Stop record must not hide OPC loss.
        fixture.Calendar.Set(fixture.Line.Id, middle, end, secondStart, secondStart.AddHours(8));
        var fullyOff = await service.GetLineOeeAsync(fixture.Request(new(2026, 11, 2)), default);
        Assert.Null(fullyOff.Day!.Oee);
        Assert.Equal(0m, fullyOff.DayMissingMinutes);
        Assert.Equal(1m, fullyOff.Month!.Oee);

        fixture.Calendar.Set(fixture.Line.Id, start, end);
        var conflicting = await service.GetLineOeeAsync(request, default);
        Assert.Null(conflicting.Day);
        Assert.Contains("có sản lượng", conflicting.Message, StringComparison.Ordinal);
    }

    private sealed class Calendar : ILineOffCalendarStore
    {
        private LineOffCalendar _calendar = LineOffCalendar.Empty;
        public void Set(Guid line, params DateTimeOffset[] bounds)
            => _calendar = new(Guid.NewGuid(), Enumerable.Range(0, bounds.Length / 2)
                .Select(index => new LineOffPeriod(Guid.NewGuid(), line, bounds[index * 2], bounds[index * 2 + 1], null,
                    "Asia/Ho_Chi_Minh", "planner", DateTimeOffset.UtcNow)).ToArray());
        public Task<LineOffCalendar> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(_calendar);
        public Task<ILineOffCalendarSession> OpenAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public OeeDbContext Db { get; }
        public WorkUnitEntity Filler { get; }
        public WorkCenterEntity Line { get; }
        private ProductEntity Product { get; }
        public Calendar Calendar { get; } = new();
        public FixedClock Clock { get; } = new();
        public DateTimeOffset Start { get; } = new(2026, 11, 1, 0, 0, 0, TimeSpan.FromHours(7));
        private Fixture(OeeDbContext db)
        {
            Db = db;
            var enterprise = DomainTestData.Create<EnterpriseEntity>(("Code", "E"), ("Name", "Test"));
            var site = DomainTestData.Create<SiteEntity>(("Enterprise", enterprise), ("Code", "S"), ("Name", "Test"), ("TimeZoneId", "Asia/Ho_Chi_Minh"));
            var area = DomainTestData.Create<AreaEntity>(("Site", site), ("Code", "A"), ("Name", "Test"));
            Line = DomainTestData.Create<WorkCenterEntity>(("Area", area), ("Code", "L1"), ("Name", "Line 1"));
            Filler = DomainTestData.Create<WorkUnitEntity>(("WorkCenter", Line), ("Code", "F01"), ("Name", "Filler"));
            Product = DomainTestData.Create<ProductEntity>(("Code", "SKU1"), ("Name", "First"), ("DefaultUom", "PCS"));
            Db.AddRange(enterprise, site, area, Line, Filler, Product);
        }
        public static async Task<Fixture?> CreateAsync()
        {
            var source = Environment.GetEnvironmentVariable("OEE_TEST_SQLSERVER");
            if (string.IsNullOrWhiteSpace(source)) return null;
            var connection = new SqlConnectionStringBuilder(source) { InitialCatalog = $"OeeDb_LineOee_{Guid.NewGuid():N}" };
            Assert.StartsWith("OeeDb_LineOee_", connection.InitialCatalog, StringComparison.Ordinal);
            var db = new OeeDbContext(new DbContextOptionsBuilder<OeeDbContext>().UseSqlServer(connection.ConnectionString).Options);
            try { await db.Database.MigrateAsync(); return new(db); }
            catch { await db.Database.EnsureDeletedAsync(); await db.DisposeAsync(); throw; }
        }
        public ProductionPlanEntity Plan(string code, DateTimeOffset start, DateTimeOffset end)
        {
            var plan = ProductionPlanEntity.Create(Filler.Id, Product.Id, code, start, end, 10000);
            Db.Add(plan);
            return plan;
        }
        public ProductionOutputEntity Output(ProductionPlanEntity plan, DateTimeOffset start, DateTimeOffset end, decimal total)
            => ProductionOutputEntity.Create(plan.Id, Filler.Id, Product.Id, start, end, 1, total, DataSourceEnum.Automatic);
        public DashboardQueryRequest Request(DateOnly date) => new(new(2026, 11, 1), date, WorkCenterId: Line.Id);
        public async ValueTask DisposeAsync()
        {
            await Db.Database.EnsureDeletedAsync();
            await Db.DisposeAsync();
            SqlConnection.ClearAllPools();
        }
    }
    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow { get; } = new(2026, 11, 3, 0, 30, 0, TimeSpan.FromHours(7)); }
}
