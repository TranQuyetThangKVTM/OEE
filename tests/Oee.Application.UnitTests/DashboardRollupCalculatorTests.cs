using Oee.Application.Modules;

namespace Oee.Application.UnitTests;

/// <summary>
/// Unit test cho phép tổng hợp Dashboard thuần bộ nhớ, không cần EF Core hay SQL Server.
/// Các test bảo vệ công thức A/P/Q/OEE, zero denominator, clamp và validation sản lượng lỗi.
/// </summary>
public sealed class DashboardRollupCalculatorTests
{
    [Fact]
    public void CalculateAggregatesRawInputsAndUsesApprovedFormula()
    {
        // Arrange + Act: đưa hai snapshot thời gian và hai dòng sản lượng vào calculator.
        var result = DashboardRollupCalculator.Calculate(
            [new DashboardSnapshotInput(480m, 420m), new DashboardSnapshotInput(480m, 380m)],
            [new DashboardProductionInput(900m, 18m, 720m), new DashboardProductionInput(100m, 2m, 80m)]);

        // Assert: kiểm tra cả dữ liệu cộng dồn và công thức ratio trên thang 0..1.
        Assert.Equal(960m, result.LoadingMinutes);
        Assert.Equal(800m, result.UtilizationMinutes);
        Assert.Equal(1_000m, result.TotalQuantity);
        Assert.Equal(980m, result.GoodQuantity);
        Assert.Equal(20m, result.RejectQuantity);
        Assert.Equal(5m / 6m, result.Availability);
        Assert.Equal(1m, result.Performance);
        Assert.Equal(.98m, result.Quality);
        Assert.Equal((5m / 6m) * .98m, result.Oee);
    }

    [Fact]
    public void CalculateReturnsNullRatiosWhenDenominatorsAreZero()
    {
        // Arrange + Act: không có thời gian/sản lượng; Assert: ratio không áp dụng là null, không NaN/Infinity.
        var result = DashboardRollupCalculator.Calculate([], []);

        Assert.Null(result.Availability);
        Assert.Null(result.Performance);
        Assert.Null(result.Quality);
        Assert.Null(result.Oee);
    }

    [Fact]
    public void CalculateClampsAbnormalRatiosWithoutProducingInfinity()
    {
        // Arrange + Act: dữ liệu bất thường làm tử số vượt mẫu số.
        var result = DashboardRollupCalculator.Calculate(
            [new DashboardSnapshotInput(100m, 120m)],
            [new DashboardProductionInput(10m, 0m, 150m)]);

        // Assert: các ratio được clamp về 1 theo rule đã duyệt.
        Assert.Equal(1m, result.Availability);
        Assert.Equal(1m, result.Performance);
        Assert.Equal(1m, result.Quality);
        Assert.Equal(1m, result.Oee);
    }

    [Fact]
    public void CalculateRejectsDefectGreaterThanTotal()
    {
        // Arrange/Act/Assert: defect > total phải bị từ chối thay vì tạo Good âm.
        Assert.Throws<ArgumentOutOfRangeException>(() => DashboardRollupCalculator.Calculate(
            [new DashboardSnapshotInput(1m, 1m)],
            [new DashboardProductionInput(2m, 3m, 1m)]));
    }
}
