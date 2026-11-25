using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Oee.Application.Common;
using Oee.Application.Contracts.Modules;
using Oee.Domain.Common;
using Oee.Domain.Entities.Oee;
using Oee.Domain.Entities.Organization;
using Oee.Domain.Entities.Production;
using Oee.Domain.PlcIntegration;
using Oee.Domain.Production;
using Oee.Infrastructure.Operations;
using Oee.Infrastructure.Persistence;
using Oee.Tests.Fixtures;

namespace Oee.Infrastructure.IntegrationTests;

/// <summary>Kiểm tra tổng ca qua nửa đêm, lịch nhà máy và counter đứng khi kết nối vẫn tốt.</summary>
public sealed class LiveOeePeriodsSqlServerTests
{
    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task PeriodsIncludePreviousPlansAndProductsAndDistinguishCounterStallFromConnectionLoss()
    {
        var source = Environment.GetEnvironmentVariable("OEE_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(source)) return;
        var connection = new SqlConnectionStringBuilder(source) { InitialCatalog = $"OeeDb_LivePeriods_{Guid.NewGuid():N}" };
        Assert.StartsWith("OeeDb_LivePeriods_", connection.InitialCatalog, StringComparison.Ordinal);
        await using var db = new OeeDbContext(new DbContextOptionsBuilder<OeeDbContext>().UseSqlServer(connection.ConnectionString).Options);
        try
        {
            await db.Database.MigrateAsync();
            var clock = new FixedClock();
            var now = clock.UtcNow; // Nhà máy VN: 13/10 00:10; UTC: 12/10 17:10.
            var enterprise = DomainTestData.Create<EnterpriseEntity>(("Code", "E"), ("Name", "Enterprise"));
            var site = DomainTestData.Create<SiteEntity>(("Enterprise", enterprise), ("Code", "VN"), ("Name", "VN"), ("TimeZoneId", "Asia/Ho_Chi_Minh"));
            var area = DomainTestData.Create<AreaEntity>(("Site", site), ("Code", "A"), ("Name", "Area"));
            var center = DomainTestData.Create<WorkCenterEntity>(("Area", area), ("Code", "C"), ("Name", "Center"));
            var unit = DomainTestData.Create<WorkUnitEntity>(("WorkCenter", center), ("Code", "M"), ("Name", "Machine"));
            var shift = DomainTestData.Create<ShiftEntity>(("Site", site), ("Code", "S3"), ("Name", "Đêm"), ("StartTime", (global::System.TimeOnly)(new(22, 0))), ("EndTime", (global::System.TimeOnly)(new(6, 0))));
            var product = DomainTestData.Create<ProductEntity>(("Code", "SKU"), ("Name", "Current"), ("DefaultUom", "unit"));
            var otherProduct = DomainTestData.Create<ProductEntity>(("Code", "OLD"), ("Name", "Previous"), ("DefaultUom", "unit"));
            var currentPlan = DomainTestData.Create<ProductionPlanEntity>(("WorkUnit", unit), ("Product", product), ("Shift", shift), ("Code", "CURRENT"), ("PlannedStart", now.AddHours(-2)), ("PlannedEnd", now.AddHours(6)), ("PlannedQuantity", 100));
            DomainTestData.Set(currentPlan, "VersionSeriesId", currentPlan.Id);
            var oldPlan = DomainTestData.Create<ProductionPlanEntity>(("WorkUnit", unit), ("Product", otherProduct), ("Shift", shift), ("Code", "OLD"), ("PlannedStart", now.AddMonths(-1)), ("PlannedEnd", now.AddHours(-2)), ("PlannedQuantity", 500));
            DomainTestData.Set(oldPlan, "VersionSeriesId", oldPlan.Id);
            var opc = DomainTestData.Create<OpcUaConnectionEntity>(("Code", "OPC"), ("Name", "OPC"), ("EndpointUrl", "opc.tcp://fake:4840"), ("IsActive", true));
            var mapping = DomainTestData.Create<PlcDeviceMappingEntity>(("OpcUaConnection", opc), ("WorkUnit", unit), ("ExternalDeviceId", "M"), ("IsActive", true));
            var runtime = DomainTestData.Create<PlcDeviceRuntimeStateEntity>(("PlcDeviceMapping", mapping), ("ConnectivityState", ConnectivityStateEnum.Connected), ("OperationalState", OperationalStateEnum.Running), ("LastDataAt", now));
            var checkpoint = DomainTestData.Create<CounterCheckpointEntity>(("PlcDeviceMapping", mapping), ("LastTotalCount", 177191), ("LastReadAt", now), ("LastCounterChangedAt", now.AddHours(-4).AddMinutes(-52)));
            db.AddRange(enterprise, site, area, center, unit, shift, product, otherProduct, currentPlan, oldPlan, opc, mapping, runtime, checkpoint);
            void Output(ProductionPlanEntity plan, DateTimeOffset end, decimal good)
                => db.ProductionOutputs.Add(DomainTestData.Create<ProductionOutputEntity>(("ProductionPlan", plan), ("WorkUnit", unit), ("Product", plan.Product), ("Shift", shift), ("PeriodStart", end.AddMinutes(-1)), ("PeriodEnd", end), ("StandardSpeedPerMinute", 100), ("TotalProducedQuantity", good + 2), ("DefectQuantity", 2), ("Source", DataSourceEnum.Automatic)));
            Output(currentPlan, now.AddMinutes(-5), 10);
            Output(currentPlan, now.AddMinutes(-20), 20); // 12/10 23:50: cùng ca, khác ngày.
            Output(oldPlan, now.AddHours(-3), 30);
            Output(oldPlan, now.AddDays(-2), 40); // Chủ nhật tuần trước.
            Output(oldPlan, new(2026, 9, 30, 10, 0, 0, TimeSpan.Zero), 50);
            Output(currentPlan, now.AddHours(1), 60); // Không tính fact tương lai.
            var shiftStart = new DateTimeOffset(2026, 10, 12, 15, 0, 0, TimeSpan.Zero);
            void Snapshot(OeePeriodTypeEnum type, decimal oee) => db.OeeSnapshots.Add(DomainTestData.Create<OeeSnapshotEntity>(("WorkUnit", unit), ("PeriodType", type), ("PeriodCode", type.ToString()), ("PeriodStart", shiftStart), ("PeriodEnd", shiftStart.AddHours(8)), ("CalculatedAt", now), ("Availability", 1), ("Performance", oee), ("Quality", 1), ("Oee", oee), ("CalculationVersion", "test"), ("InputHash", new string(type == OeePeriodTypeEnum.Shift ? 'S' : 'D', 64))));
            Snapshot(OeePeriodTypeEnum.Day, .99m);
            Snapshot(OeePeriodTypeEnum.Shift, .8m);
            db.ProductionReconciliationItems.AddRange(
                ProductionReconciliationItemEntity.Create(unit.Id, "gap-current", now.AddMinutes(-1), 50, 0,
                    ProductionOutputAttributionRules.CollectionGapReason),
                ProductionReconciliationItemEntity.Create(unit.Id, "gap-previous-month", now.AddMonths(-1), 1000, 0,
                    ProductionOutputAttributionRules.CollectionGapReason),
                ProductionReconciliationItemEntity.Create(unit.Id, "no-plan", now.AddMinutes(-1), 1000, 0, "NO_ACTIVE_PLAN"));
            await db.SaveChangesAsync();
            var query = new LiveOeeQueryService(db, clock);
            var machine = Assert.Single((await query.GetAsync(new(), default)).Machines);
            var periods = Assert.IsType<LiveProductionPeriods>(machine.ProductionPeriods);
            Assert.Equal("S3", periods.ShiftCode);
            Assert.Equal(new DateOnly(2026, 10, 13), periods.LocalDate);
            Assert.Equal(42, periods.WeekNumber);
            Assert.Equal(30m, periods.ShiftQuantity);
            Assert.Equal(10m, periods.DayQuantity);
            Assert.Equal(60m, periods.WeekQuantity);
            Assert.Equal(100m, periods.MonthQuantity);
            Assert.Equal(50m, machine.UnassignedGapQuantity);
            Assert.Equal(now.AddMinutes(-1), machine.LastCollectionGapAt);
            Assert.Equal(30m, periods.CurrentProductQuantity);
            Assert.Equal(.8m, machine.Oee); // Không lấy snapshot DAY cùng CalculatedAt.
            Assert.False(machine.IsStale);
            Assert.True(machine.IsCounterStalled);
            Assert.Equal(17520, machine.CounterIdleSeconds);
            DomainTestData.Set(runtime, "OperationalState", OperationalStateEnum.Stopped);
            await db.SaveChangesAsync();
            Assert.False(Assert.Single((await query.GetAsync(new(), default)).Machines).IsCounterStalled);

            // Chưa có sản lượng: P=0 khi đã có thời gian chạy, nhưng Q/OEE chưa xác định.
            var currentSnapshot = await db.OeeSnapshots.SingleAsync(x => x.PeriodType == OeePeriodTypeEnum.Shift);
            DomainTestData.Set(currentSnapshot, "LoadingMinutes", 10);
            DomainTestData.Set(currentSnapshot, "UtilizationMinutes", 10);
            DomainTestData.Set(currentSnapshot, "TotalQuantity", 0);
            DomainTestData.Set(currentSnapshot, "DefectQuantity", 0);
            DomainTestData.Set(currentSnapshot, "Availability", 1);
            DomainTestData.Set(currentSnapshot, "Performance", 0);
            DomainTestData.Set(currentSnapshot, "Quality", 0);
            DomainTestData.Set(currentSnapshot, "Oee", 0);
            DomainTestData.Set(currentSnapshot, "AnomalyFlags", "[\"NotApplicable\"]");
            await db.SaveChangesAsync();
            machine = Assert.Single((await query.GetAsync(new(), default)).Machines);
            Assert.Equal(1, machine.Availability);
            Assert.Equal(0, machine.Performance);
            Assert.Null(machine.Quality);
            Assert.Null(machine.Oee);

            // Chưa có thời gian: cả A/P/Q/OEE đều chưa xác định, không khởi tạo giả thành 0 hoặc 100.
            DomainTestData.Set(currentSnapshot, "LoadingMinutes", 0);
            DomainTestData.Set(currentSnapshot, "UtilizationMinutes", 0);
            await db.SaveChangesAsync();
            machine = Assert.Single((await query.GetAsync(new(), default)).Machines);
            Assert.Null(machine.Availability);
            Assert.Null(machine.Performance);
            Assert.Null(machine.Quality);
            Assert.Null(machine.Oee);

            // Có dữ liệu nhưng tất cả đều lỗi: Q/OEE bằng 0 là kết quả hợp lệ.
            DomainTestData.Set(currentSnapshot, "LoadingMinutes", 10);
            DomainTestData.Set(currentSnapshot, "UtilizationMinutes", 10);
            DomainTestData.Set(currentSnapshot, "TotalQuantity", 10);
            DomainTestData.Set(currentSnapshot, "DefectQuantity", 10);
            DomainTestData.Set(currentSnapshot, "Performance", .5m);
            DomainTestData.Set(currentSnapshot, "AnomalyFlags", "[]");
            await db.SaveChangesAsync();
            machine = Assert.Single((await query.GetAsync(new(), default)).Machines);
            Assert.Equal(.5m, machine.Performance);
            Assert.Equal(0, machine.Quality);
            Assert.Equal(0, machine.Oee);
            DomainTestData.Set(runtime, "OperationalState", OperationalStateEnum.Running);
            DomainTestData.Set(runtime, "ConnectivityState", ConnectivityStateEnum.NoData);
            await db.SaveChangesAsync();
            machine = Assert.Single((await query.GetAsync(new(), default)).Machines);
            Assert.True(machine.IsStale);
            Assert.False(machine.IsCounterStalled);
            DomainTestData.Set(runtime, "ConnectivityState", ConnectivityStateEnum.Connected);
            DomainTestData.Set(checkpoint, "LastCounterChangedAt", now);
            await db.SaveChangesAsync();
            Assert.False(Assert.Single((await query.GetAsync(new(), default)).Machines).IsCounterStalled);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
            SqlConnection.ClearAllPools();
        }
    }

    private sealed class FixedClock : IClock { public DateTimeOffset UtcNow => new(2026, 10, 12, 17, 10, 0, TimeSpan.Zero); }
}
