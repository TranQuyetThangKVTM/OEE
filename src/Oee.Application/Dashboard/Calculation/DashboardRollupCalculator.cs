namespace Oee.Application.Modules;

/// <summary>
/// Pure Application service cộng gộp dữ liệu nhiều máy/kỳ thành KPI Dashboard nhất quán.
/// </summary>
/// <remarks>
/// Lớp không dùng EF Core, clock hay UI. Query service chuẩn bị dữ liệu, calculator kiểm tra đầu vào
/// rồi áp dụng cùng ý nghĩa công thức OEE. Mẫu số bằng 0 trả <see langword="null"/> để UI hiển thị
/// “chưa có dữ liệu”, không sinh NaN/Infinity.
/// </remarks>
public static class DashboardRollupCalculator
{
    /// <summary>Tính tổng Loading/Utilization/sản lượng và các tỷ lệ A/P/Q/OEE.</summary>
    /// <param name = "snapshots">Các khoảng thời gian cần tổng hợp.</param>
    /// <param name = "production">Các dòng sản lượng và ideal run time tương ứng.</param>
    /// <returns>KPI tổng hợp, với tỷ lệ đã clamp trong miền 0..1.</returns>
    /// <exception cref = "ArgumentNullException">Khi một tập đầu vào là null.</exception>
    /// <exception cref = "ArgumentOutOfRangeException">Khi thời gian/sản lượng âm hoặc defect lớn hơn total.</exception>
    public static DashboardRollupResult Calculate(
        IEnumerable<DashboardSnapshotInput> snapshots,
        IEnumerable<DashboardProductionInput> production)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        ArgumentNullException.ThrowIfNull(production);
        // Materialize một lần để validation và phép Sum cùng nhìn một bộ dữ liệu ổn định.
        var snapshotRows = snapshots.ToArray();
        var productionRows = production.ToArray();

        if (snapshotRows.Any(row => row.LoadingMinutes < 0 || row.UtilizationMinutes < 0))
        {
            throw new ArgumentOutOfRangeException(nameof(snapshots), "Dashboard time inputs cannot be negative.");
        }

        if (productionRows.Any(row => row.TotalQuantity < 0 || row.DefectQuantity < 0 || row.DefectQuantity > row.TotalQuantity
                || row.IdealRunMinutes < 0))
        {
            throw new ArgumentOutOfRangeException(nameof(production), "Dashboard production inputs are invalid.");
        }

        // Cộng số tuyệt đối trước rồi mới chia; cách này phản ánh đúng trọng số của từng máy/kỳ.
        var loading = snapshotRows.Sum(row => row.LoadingMinutes);
        var utilization = snapshotRows.Sum(row => row.UtilizationMinutes);
        var total = productionRows.Sum(row => row.TotalQuantity);
        var reject = productionRows.Sum(row => row.DefectQuantity);
        var good = total - reject;
        var idealRun = productionRows.Sum(row => row.IdealRunMinutes);
        // A = Utilization / Loading; P = Ideal run / Utilization; Q = Good / Total.
        var availability = DivideAndClamp(utilization, loading);
        var performance = DivideAndClamp(idealRun, utilization);
        var quality = DivideAndClamp(good, total);
        // Chỉ có OEE khi cả ba thành phần đều xác định; kết quả vẫn ở thang 0..1.
        decimal? oee = availability.HasValue && performance.HasValue && quality.HasValue
            ? availability.Value * performance.Value * quality.Value
            : null;

        return new DashboardRollupResult(loading, utilization, idealRun, total, good, reject, availability, performance, quality, oee);
    }

    // Clamp bảo vệ Dashboard trước dữ liệu ngoại lệ; null biểu thị mẫu số không có ý nghĩa.
    private static decimal? DivideAndClamp(
        decimal numerator,
        decimal denominator)
        => denominator == 0 ? null : Math.Clamp(numerator / denominator, 0, 1);
}
