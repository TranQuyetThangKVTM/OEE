using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Oee.Application.Common;
using Oee.Application.Contracts.Reporting;
using Oee.Domain.Common;
using Oee.Domain.Downtime;
using Oee.Domain.Entities.Oee;
using Oee.Domain.Entities.Organization;
using Oee.Domain.Entities.Production;
using Oee.Domain.PlcIntegration;
using Oee.Infrastructure.Persistence;
using Oee.Infrastructure.Reporting;
using Oee.Tests.Fixtures;

namespace Oee.Infrastructure.IntegrationTests;

/// <summary>
/// Integration Test SQL Server bảo đảm các báo cáo dùng cùng dữ liệu OEE, timezone Site và logic ca qua nửa đêm.
/// </summary>
public sealed class ReportingQuerySqlServerTests
{
    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task AllReportKindsUseRealDataSiteTimeZoneAndCrossMidnightShift()
    {
        // Arrange: tạo database test riêng và bỏ qua khi CI chưa cung cấp SQL Server.
        var baseConnectionString = Environment.GetEnvironmentVariable("OEE_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(baseConnectionString)) return;

        var databaseName = $"OeeDb_ReportTests_{Guid.NewGuid():N}";
        var connection = new SqlConnectionStringBuilder(baseConnectionString) { InitialCatalog = databaseName };
        Assert.StartsWith("OeeDb_ReportTests_", connection.InitialCatalog, StringComparison.Ordinal);
        var options = new DbContextOptionsBuilder<OeeDbContext>().UseSqlServer(connection.ConnectionString).Options;
        await using var db = new OeeDbContext(options);
        try
        {
            // Arrange: seed ca S3 22:00–06:00, output, Breakdown và snapshot DAY/SHIFT từ calculation service thật.
            await db.Database.MigrateAsync();
            var enterprise = DomainTestData.Create<EnterpriseEntity>(("Code", "ENT-R"), ("Name", "Enterprise"), ("Status", 1));
            var site = DomainTestData.Create<SiteEntity>(("EnterpriseId", enterprise.Id), ("Code", "SITE-R"), ("Name", "Factory"), ("TimeZoneId", "Asia/Ho_Chi_Minh"), ("Status", 1));
            var area = DomainTestData.Create<AreaEntity>(("SiteId", site.Id), ("Code", "PROC-R"), ("Name", "Area"), ("Status", 1));
            var group = DomainTestData.Create<WorkCenterEntity>(("AreaId", area.Id), ("Code", "LINE-R"), ("Name", "Line"), ("Status", 1));
            var unit = DomainTestData.Create<WorkUnitEntity>(("WorkCenterId", group.Id), ("Code", "M-R01"), ("Name", "Machine"), ("Status", 1));
            var product = DomainTestData.Create<ProductEntity>(("Code", "SKU-R"), ("Name", "Product"), ("DefaultUom", "unit"), ("Status", 1));
            var shift = DomainTestData.Create<ShiftEntity>(("SiteId", site.Id), ("Code", "S3"), ("Name", "Night"), ("StartTime", new TimeOnly(22, 0)), ("EndTime", new TimeOnly(6, 0)), ("Status", 1));
            var reason = DomainTestData.Create<DowntimeReasonEntity>(("Code", "DR-BREAK"), ("Name", "Breakdown"), ("Category", DowntimeCategoryEnum.AvailabilityLoss), ("Kind", DowntimeKindEnum.Breakdown), ("Status", 1));
            var shiftStart = new DateTimeOffset(2026, 8, 2, 22, 0, 0, TimeSpan.FromHours(7));
            var shiftEnd = new DateTimeOffset(2026, 8, 3, 6, 0, 0, TimeSpan.FromHours(7));
            var plan = DomainTestData.Create<ProductionPlanEntity>(("WorkUnitId", unit.Id), ("ProductId", product.Id), ("ShiftId", shift.Id), ("Code", "PLAN-R"), ("PlannedStart", shiftStart), ("PlannedEnd", shiftEnd), ("PlannedQuantity", 120));
            DomainTestData.Set(plan, "VersionSeriesId", plan.Id);
            var output = DomainTestData.Create<ProductionOutputEntity>(("ProductionPlanId", plan.Id), ("WorkUnitId", unit.Id), ("ProductId", product.Id), ("ShiftId", shift.Id), ("PeriodStart", new DateTimeOffset(2026, 8, 3, 0, 30, 0, TimeSpan.FromHours(7))), ("PeriodEnd", new DateTimeOffset(2026, 8, 3, 1, 30, 0, TimeSpan.FromHours(7))), ("StandardSpeedPerMinute", 10), ("TotalProducedQuantity", 100), ("DefectQuantity", 5), ("Source", DataSourceEnum.Automatic));
            var stop = DomainTestData.Create<DowntimeEventEntity>(("WorkUnitId", unit.Id), ("ProductionOutputId", output.Id), ("DowntimeReasonId", reason.Id), ("Category", DowntimeCategoryEnum.AvailabilityLoss), ("Kind", DowntimeKindEnum.Breakdown), ("StartAt", new DateTimeOffset(2026, 8, 3, 0, 45, 0, TimeSpan.FromHours(7))), ("EndAt", new DateTimeOffset(2026, 8, 3, 0, 55, 0, TimeSpan.FromHours(7))), ("Source", DataSourceEnum.Automatic));
            db.AddRange(enterprise, site, area, group, unit, product, shift, reason, plan, output, stop);
            await db.SaveChangesAsync();

            var clock = new FixedClock();
            var snapshots = ApplicationServiceFactory.Snapshots(db, clock);
            var dayStart = new DateTimeOffset(2026, 8, 2, 0, 0, 0, TimeSpan.FromHours(7));
            var dayEnd = dayStart.AddDays(1);
            await snapshots.RecalculateAsync(unit.Id, OeePeriodTypeEnum.Day, "DAY-20260802", dayStart, dayEnd, CancellationToken.None);
            await snapshots.RecalculateAsync(unit.Id, OeePeriodTypeEnum.Shift, "SHIFT-S3-20260802", shiftStart, shiftEnd, CancellationToken.None);
            db.ChangeTracker.Clear();

            // Act: tạo query service với đơn giá cấu hình; UI và PDF đều nhận cùng ReportData.
            var service = new ReportAnalysisQueryService(db, clock);
            var filters = await service.GetFilterOptionsAsync(CancellationToken.None);
            Assert.Contains(filters.WorkUnits, x => x.Id == unit.Id);
            Assert.Contains(filters.Shifts, x => x.Id == shift.Id);

            // Assert: mọi loại báo cáo có dữ liệu thật, đúng timezone Site và không dùng số mẫu trong production path.
            foreach (var kind in Enum.GetValues<ReportKind>())
            {
                var report = await service.QueryAsync(new ReportQuery(kind, new DateOnly(2026, 8, 2),
                    new DateOnly(2026, 8, 3), WorkUnitId: unit.Id, PageSize: 25), CancellationToken.None);
                Assert.Equal("Asia/Ho_Chi_Minh", report.TimeZoneId);
                Assert.True(report.HasData, kind.ToString());
                Assert.NotEmpty(report.Rows);
            }

            // Act & Assert: báo cáo ca đọc đúng snapshot S3 và Quality lưu theo tỷ lệ 0..1.
            var shiftReport = await service.QueryAsync(new ReportQuery(ReportKind.ShiftKpi,
                new DateOnly(2026, 8, 2), new DateOnly(2026, 8, 2), WorkUnitId: unit.Id,
                ProductId: product.Id, ShiftId: shift.Id, PageSize: 10), CancellationToken.None);
            var shiftRow = Assert.Single(shiftReport.Rows);
            Assert.Equal("SHIFT-S3-20260802", shiftRow.Code);
            Assert.Equal(100, shiftRow.Total);
            Assert.Equal(5, shiftRow.Defect);
            Assert.Equal(.95m, shiftRow.Quality);

            // Act & Assert: Reliability chỉ tính event Breakdown/Failure, ở đây MTTR bằng mười phút.
            var reliability = await service.QueryAsync(new ReportQuery(ReportKind.Reliability,
                new DateOnly(2026, 8, 2), new DateOnly(2026, 8, 2), WorkUnitId: unit.Id,
                ShiftId: shift.Id), CancellationToken.None);
            var reliabilityRow = Assert.Single(reliability.Rows);
            Assert.Equal(1, reliabilityRow.EventCount);
            Assert.Equal(10, reliabilityRow.MttrMinutes);
            Assert.Equal(50, reliabilityRow.UptimeMinutes);
            Assert.Equal(10, reliabilityRow.DowntimeMinutes);
            Assert.Equal(50m / 60m, reliabilityRow.Availability);

            // Act & Assert: render chính ReportData trên thành file PDF với font tiếng Việt nhúng.
            var pdf = new ReportExportService()
                .RenderPdf(shiftReport);
            Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(pdf, 0, 5));
            Assert.True(pdf.Length > 5_000);

            // Unlinked stops still belong to the selected overnight shift by time.
            db.DowntimeEvents.AddRange(
                DomainTestData.Create<DowntimeEventEntity>(("WorkUnitId", unit.Id), ("DowntimeReasonId", reason.Id), ("Kind", DowntimeKindEnum.Failure), ("Category", DowntimeCategoryEnum.AvailabilityLoss), ("StartAt", new DateTimeOffset(2026, 8, 3, 1, 50, 0, TimeSpan.FromHours(7))), ("Source", DataSourceEnum.Automatic)),
                DomainTestData.Create<DowntimeEventEntity>(("WorkUnitId", unit.Id), ("DowntimeReasonId", reason.Id), ("Kind", DowntimeKindEnum.Planned), ("Category", DowntimeCategoryEnum.AvailabilityLoss), ("StartAt", shiftStart.AddMinutes(5)), ("EndAt", shiftStart.AddMinutes(10)), ("Source", DataSourceEnum.Manual)),
                DomainTestData.Create<DowntimeEventEntity>(("WorkUnitId", unit.Id), ("DowntimeReasonId", reason.Id), ("Kind", DowntimeKindEnum.Failure), ("Category", DowntimeCategoryEnum.AvailabilityLoss), ("StartAt", shiftStart.AddHours(-7)), ("EndAt", shiftStart.AddHours(-6)), ("Source", DataSourceEnum.Manual)),
                DomainTestData.Create<DowntimeEventEntity>(("WorkUnitId", unit.Id), ("DowntimeReasonId", reason.Id), ("Kind", DowntimeKindEnum.Failure), ("Category", DowntimeCategoryEnum.AvailabilityLoss), ("StartAt", shiftStart.AddHours(5)), ("EndAt", shiftStart.AddHours(6)), ("Source", DataSourceEnum.Automatic)));
            await db.SaveChangesAsync();
            var nightQuery = new ReportQuery(ReportKind.Reliability, new DateOnly(2026, 8, 2),
                new DateOnly(2026, 8, 2), WorkUnitId: unit.Id, ShiftId: shift.Id);
            var night = await service.QueryAsync(nightQuery, CancellationToken.None);
            var nightRow = Assert.Single(night.Rows);
            Assert.Equal(2, nightRow.EventCount);
            Assert.Equal(20, nightRow.DowntimeMinutes);
            Assert.Equal(50, nightRow.UptimeMinutes);
            Assert.Equal(10, nightRow.MttrMinutes);
            Assert.NotNull(night.Timeline);
            Assert.Equal(shiftStart, night.Timeline.Start);
            Assert.Equal(shiftEnd, night.Timeline.End);
            Assert.Contains(Assert.Single(night.Timeline.Rows).Segments, x => x.State == EquipmentTimelineState.Future);
            var eventsReport = await service.QueryAsync(nightQuery with { Kind = ReportKind.DowntimeEvents }, CancellationToken.None);
            Assert.Equal(3, eventsReport.TotalCount);
            Assert.Equal(25, eventsReport.Metrics.Single(x => x.Key == "total").Value);

            var connectionConfig = DomainTestData.Create<OpcUaConnectionEntity>(("Code", "CONN-R"), ("Name", "Connection"), ("EndpointUrl", "opc.tcp://test:4840"), ("SecurityMode", "None"), ("SecurityPolicy", "None"), ("IsActive", true));
            var mapping = DomainTestData.Create<PlcDeviceMappingEntity>(("WorkUnitId", unit.Id), ("OpcUaConnectionId", connectionConfig.Id), ("ExternalDeviceId", "PLC-R"), ("CounterMode", CounterModeEnum.Cumulative), ("IsActive", true));
            db.AddRange(connectionConfig, mapping,
                DomainTestData.Create<PlcDeviceRuntimeStateEntity>(("PlcDeviceMappingId", mapping.Id), ("LastDataAt", clock.UtcNow), ("OperationalState", OperationalStateEnum.Stopped), ("ConnectivityState", ConnectivityStateEnum.Connected)));
            var runStart = shiftStart.AddHours(1);
            db.PlcIntegrationEvents.AddRange(
                StateEvent(mapping.Id, runStart, "StateTransition", "Stopped -> Running"),
                StateEvent(mapping.Id, runStart.AddMinutes(30), "Connectivity", "Disconnected"),
                StateEvent(mapping.Id, runStart.AddMinutes(40), "Connectivity", "Connected"),
                StateEvent(mapping.Id, runStart.AddHours(1), "StateTransition", "Stopped -> Running"),
                StateEvent(mapping.Id, runStart.AddHours(1).AddMinutes(100), "StateTransition", "Running -> Stopped"));
            await db.SaveChangesAsync();
            var stateReport = await service.QueryAsync(nightQuery, CancellationToken.None);
            Assert.Equal(120, Assert.Single(stateReport.Rows).UptimeMinutes);
            var historySegments = Assert.Single(stateReport.Timeline!.Rows).Segments;
            Assert.Contains(historySegments, x => x.State == EquipmentTimelineState.Running &&
                x.Start == runStart && x.End == runStart.AddMinutes(30)); // Running without any counter output.
            Assert.DoesNotContain(historySegments, x => x.State == EquipmentTimelineState.Running &&
                x.Start < runStart.AddHours(1) && x.End > runStart.AddMinutes(30)); // No extrapolation through disconnect/reconnect.
        }
        finally
        {
            // Cleanup database test tách biệt.
            await db.Database.EnsureDeletedAsync();
            SqlConnection.ClearAllPools();
        }
    }

    [SqlServerTheory]
    [InlineData(ReportKind.WeeklyKpi, "2026-W01", "2026-W02")]
    [InlineData(ReportKind.MonthlyKpi, "2025-12", "2026-01")]
    [Trait("Category", "SqlServer")]
    public async Task PeriodKpiAggregatesRawTimesAcrossDevicesAndKeepsFullTrendWhenPaging(
        ReportKind kind, string firstPeriod, string secondPeriod)
    {
        var baseConnection = Environment.GetEnvironmentVariable("OEE_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(baseConnection)) return;
        var connection = new SqlConnectionStringBuilder(baseConnection)
        { InitialCatalog = $"OeeDb_ReportTests_{Guid.NewGuid():N}" };
        var options = new DbContextOptionsBuilder<OeeDbContext>().UseSqlServer(connection.ConnectionString).Options;
        await using var db = new OeeDbContext(options);
        try
        {
            await db.Database.MigrateAsync();
            var enterprise = DomainTestData.Create<EnterpriseEntity>(("Code", "E"), ("Name", "Enterprise"), ("Status", 1));
            var site = DomainTestData.Create<SiteEntity>(("Code", "S"), ("Name", "Site"), ("EnterpriseId", enterprise.Id), ("TimeZoneId", "Asia/Ho_Chi_Minh"), ("Status", 1));
            var area = DomainTestData.Create<AreaEntity>(("Code", "A"), ("Name", "Area"), ("SiteId", site.Id), ("Status", 1));
            var center = DomainTestData.Create<WorkCenterEntity>(("Code", "C"), ("Name", "Center"), ("AreaId", area.Id), ("Status", 1));
            var unit1 = DomainTestData.Create<WorkUnitEntity>(("Code", "M1"), ("Name", "Machine 1"), ("WorkCenterId", center.Id), ("Status", 1));
            var unit2 = DomainTestData.Create<WorkUnitEntity>(("Code", "M2"), ("Name", "Machine 2"), ("WorkCenterId", center.Id), ("Status", 1));
            db.AddRange(enterprise, site, area, center, unit1, unit2);
            var first = new DateTimeOffset(2025, 12, 29, 0, 0, 0, TimeSpan.FromHours(7));
            db.OeeSnapshots.AddRange(
                PeriodSnapshot(unit1.Id, first, 100, 50, 100),
                PeriodSnapshot(unit2.Id, first, 900, 900, 900),
                PeriodSnapshot(unit1.Id, first.AddDays(7), 100, 50, 100));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            var service = new ReportAnalysisQueryService(db, new FixedClock());
            var request = new ReportQuery(kind, new DateOnly(2025, 12, 29), new DateOnly(2026, 1, 5),
                SortBy: "code", Descending: false, PageSize: 1);
            var report = await service.QueryAsync(request, CancellationToken.None);
            Assert.Equal(2, report.TotalCount);
            var row = Assert.Single(report.Rows);
            Assert.Null(row.WorkUnitId);
            Assert.Equal(firstPeriod, row.Code);
            Assert.Equal(1000, row.LoadingMinutes);
            Assert.Equal(1000, row.Good);
            Assert.Equal(.95m, row.Oee); // (50+900)/(100+900), not the average .75.
            Assert.Equal(new[] { firstPeriod, secondPeriod }, report.Trend.Select(x => x.Label));
            Assert.Equal(.725m, report.Metrics.Single(x => x.Key == "oee-average").Value);
            Assert.Equal(1100, report.Metrics.Single(x => x.Key == "good").Value);
            var page2 = await service.QueryAsync(request with { Page = 2 }, CancellationToken.None);
            Assert.Equal(secondPeriod, Assert.Single(page2.Rows).Code);
            Assert.Equal(report.Trend, page2.Trend);
            var scoped = await service.QueryAsync(request with { WorkUnitId = unit1.Id }, CancellationToken.None);
            Assert.Equal(.5m, Assert.Single(scoped.Rows).Oee);
            var daily = await service.QueryAsync(request with { Kind = ReportKind.DailyOee, PageSize = 25 }, CancellationToken.None);
            Assert.Equal(3, daily.TotalCount);
            Assert.All(daily.Rows, x => Assert.Contains("Machine", x.WorkUnit, StringComparison.Ordinal));
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
            SqlConnection.ClearAllPools();
        }
    }

    private static OeeSnapshotEntity PeriodSnapshot(Guid unitId, DateTimeOffset start, decimal loading, decimal ideal, decimal total) => DomainTestData.Create<OeeSnapshotEntity>(("WorkUnitId", unitId), ("PeriodType", OeePeriodTypeEnum.Day), ("PeriodCode", "DAY-" + start.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture)), ("PeriodStart", start), ("PeriodEnd", start.AddDays(1)), ("TotalMinutes", loading), ("LoadingMinutes", loading), ("UtilizationMinutes", loading), ("NetUtilizationMinutes", ideal), ("TotalQuantity", total), ("GoodQuantity", total), ("Availability", 1), ("Quality", 1), ("Performance", ideal / loading), ("Oee", ideal / loading), ("ValueAddedMinutes", ideal), ("CalculatedAt", start.AddDays(1)), ("CalculationVersion", "test"), ("InputHash", new string('B', 64)));

    private static PlcIntegrationEventEntity StateEvent(Guid mappingId, DateTimeOffset at, string kind, string message) => DomainTestData.Create<PlcIntegrationEventEntity>(("PlcDeviceMappingId", mappingId), ("OccurredAt", at), ("Kind", kind), ("Message", message), ("Level", IntegrationEventLevelEnum.Information));

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 8, 2, 19, 0, 0, TimeSpan.Zero);
    }
}
