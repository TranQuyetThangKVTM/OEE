using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace Oee.DatabaseMigrator;

/// <summary>Một vấn đề chất lượng dữ liệu legacy cùng số bản ghi và hành động xử lý đề xuất.</summary>
public sealed record LegacyQualityFinding(string Code, string Severity, long Count, string Action);

/// <summary>
/// Kết quả preflight chỉ đọc: phiên bản schema, số dòng, lỗi dữ liệu và trạng thái xác nhận backup.
/// </summary>
public sealed record LegacyPreflightReport(DateTimeOffset GeneratedAt, string SourceDatabase, string? MigrationVersion,
    IReadOnlyDictionary<string, long> TableCounts, IReadOnlyCollection<LegacyQualityFinding> Findings,
    bool SchemaCompatible, bool BackupConfirmed);

/// <summary>
/// Đọc database legacy để đánh giá khả năng migration mà không sửa nguồn hoặc database đích.
/// </summary>
/// <remarks>
/// Connection string nguồn do DatabaseMigrator nhận từ secret riêng. Danh sách bảng là allow-list cố định;
/// tên bảng do người dùng gửi không được nối trực tiếp vào SQL.
/// </remarks>
public sealed class LegacyPreflightService(string sourceConnectionString)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly string[] RequiredTables = ["Enterprises", "Sites", "Processes", "WorkUnitGroups", "WorkUnits", "Products", "Shifts", "WorkUnitStandards", "DowntimeReasons", "ProductionPlans", "ProductionOutputs", "DowntimeEvents", "OeeSnapshots", "OeeTargets", "PlcDeviceMappings", "PlcFaultCodeMappings"];

    /// <summary>Kiểm tra schema, đếm dữ liệu và phát hiện các nhóm lỗi migration quan trọng.</summary>
    /// <param name="backupConfirmed">Cho biết quy trình bên ngoài đã xác nhận có backup; method không tự tạo backup.</param>
    /// <param name="ct">Dừng kết nối/truy vấn SQL khi tiến trình bị hủy.</param>
    /// <returns>Báo cáo typed; method không import hay cập nhật bất kỳ bản ghi nào.</returns>
    /// <exception cref="InvalidOperationException">Connection nguồn không chỉ rõ catalog.</exception>
    public async Task<LegacyPreflightReport> RunAsync(bool backupConfirmed, CancellationToken ct)
    {
        // Bước 1: parse connection string bằng SqlConnectionStringBuilder và yêu cầu tên database rõ ràng.
        var builder = new SqlConnectionStringBuilder(sourceConnectionString);
        if (string.IsNullOrWhiteSpace(builder.InitialCatalog)) throw new InvalidOperationException("Legacy source connection must specify a database.");
        // Bước 2: mở một kết nối nguồn và đọc danh sách bảng từ metadata sys.tables.
        await using var connection = new SqlConnection(builder.ConnectionString); await connection.OpenAsync(ct);
        var existing = await ReadNames(connection, "SELECT [name] FROM sys.tables", ct);
        var missing = RequiredTables.Where(table => !existing.Contains(table)).ToArray();
        // Bước 3: chỉ đếm các bảng thuộc RequiredTables; tên nội suy xuất phát từ allow-list nội bộ.
        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var table in RequiredTables.Where(existing.Contains)) counts[table] = await Scalar(connection, $"SELECT COUNT_BIG(*) FROM [dbo].[{table}]", ct);
        var version = existing.Contains("__EFMigrationsHistory") ? await TextScalar(connection, "SELECT TOP(1) [MigrationId] FROM [dbo].[__EFMigrationsHistory] ORDER BY [MigrationId] DESC", ct) : null;
        // Bước 4: thiếu schema là lỗi Critical; chỉ chạy bộ query chất lượng khi đủ toàn bộ bảng bắt buộc.
        var findings = new List<LegacyQualityFinding>();
        if (missing.Length > 0) findings.Add(new("SCHEMA_MISSING", "Critical", missing.Length, "Bổ sung/đúng legacy schema: " + string.Join(", ", missing)));
        if (missing.Length == 0)
        {
            // Các query bên dưới chỉ COUNT; chúng không sửa dữ liệu và không tự suy đoán cách liên kết output với plan.
            await Add(connection, findings, "OUTPUT_WITHOUT_PLAN", "Action", "SELECT COUNT_BIG(*) FROM dbo.ProductionOutputs WHERE ProductionPlanId IS NULL", "Đưa vào ProductionReconciliationItems; không tự đoán plan.", ct);
            await Add(connection, findings, "UNKNOWN_DOWNTIME", "Action", "SELECT COUNT_BIG(*) FROM dbo.DowntimeEvents d JOIN dbo.DowntimeReasons r ON r.Id=d.DowntimeReasonId WHERE r.Code='DR-UNKNOWN'", "Tạo action list reclassification.", ct);
            await Add(connection, findings, "UNKNOWN_FAULT_MAPPING", "Action", "SELECT COUNT_BIG(*) FROM dbo.PlcFaultCodeMappings f JOIN dbo.DowntimeReasons r ON r.Id=f.DowntimeReasonId WHERE r.Code='DR-UNKNOWN'", "Tạo action list fault mapping.", ct);
            await Add(connection, findings, "INVALID_OUTPUT_RANGE", "Critical", "SELECT COUNT_BIG(*) FROM dbo.ProductionOutputs WHERE PeriodEnd<=PeriodStart", "Sửa hoặc quarantine.", ct);
            await Add(connection, findings, "DEFECT_GT_TOTAL", "Critical", "SELECT COUNT_BIG(*) FROM dbo.ProductionOutputs WHERE DefectQuantity>TotalProducedQuantity OR DefectQuantity<0", "Sửa hoặc quarantine.", ct);
            await Add(connection, findings, "ORPHAN_OUTPUT_WORKUNIT", "Critical", "SELECT COUNT_BIG(*) FROM dbo.ProductionOutputs o LEFT JOIN dbo.WorkUnits w ON w.Id=o.WorkUnitId WHERE w.Id IS NULL", "Khôi phục master hoặc quarantine.", ct);
            await Add(connection, findings, "OUTPUT_OVERLAP", "Warning", "SELECT COUNT_BIG(*) FROM dbo.ProductionOutputs a JOIN dbo.ProductionOutputs b ON a.WorkUnitId=b.WorkUnitId AND a.Id<b.Id AND a.PeriodStart<b.PeriodEnd AND b.PeriodStart<a.PeriodEnd", "Review overlap trước khi canonical import.", ct);
            await Add(connection, findings, "SNAPSHOT_DUPLICATE", "Critical", "SELECT COALESCE(SUM(c-1),0) FROM (SELECT COUNT_BIG(*) c FROM dbo.OeeSnapshots GROUP BY WorkUnitId,PeriodType,PeriodCode,PeriodStart,PeriodEnd HAVING COUNT_BIG(*)>1) x", "Chọn CalculationVersion/evidence và deduplicate.", ct);
            await Add(connection, findings, "STANDARD_OVERLAP", "Critical", "SELECT COUNT_BIG(*) FROM dbo.WorkUnitStandards a JOIN dbo.WorkUnitStandards b ON a.Id<b.Id AND a.WorkUnitId=b.WorkUnitId AND a.ProductId=b.ProductId AND a.EffectiveFrom<=ISNULL(b.EffectiveTo,'9999-12-31') AND b.EffectiveFrom<=ISNULL(a.EffectiveTo,'9999-12-31')", "Resolve effective ranges.", ct);
            await Add(connection, findings, "TARGET_OVERLAP", "Critical", "SELECT COUNT_BIG(*) FROM dbo.OeeTargets a JOIN dbo.OeeTargets b ON a.Id<b.Id AND ISNULL(a.WorkUnitId,'00000000-0000-0000-0000-000000000000')=ISNULL(b.WorkUnitId,'00000000-0000-0000-0000-000000000000') AND ISNULL(a.SiteId,'00000000-0000-0000-0000-000000000000')=ISNULL(b.SiteId,'00000000-0000-0000-0000-000000000000') AND a.PeriodType=b.PeriodType AND a.EffectiveFrom<=ISNULL(b.EffectiveTo,'9999-12-31') AND b.EffectiveFrom<=ISNULL(a.EffectiveTo,'9999-12-31')", "Resolve target precedence/effective ranges.", ct);
        }
        // GeneratedAt dùng UTC; caller quyết định có dừng quy trình dựa trên SchemaCompatible/Critical finding.
        return new(DateTimeOffset.UtcNow, builder.InitialCatalog, version, counts, findings, missing.Length == 0, backupConfirmed);
    }

    /// <summary>Serialize report dạng JSON có thụt dòng để lưu làm evidence preflight.</summary>
    /// <param name="report">Báo cáo typed không chứa credential.</param>
    /// <returns>JSON chỉ chứa metadata/count/finding của lần kiểm tra.</returns>
    public static string Serialize(LegacyPreflightReport report) => JsonSerializer.Serialize(report, JsonOptions);

    // Các helper dưới đây đóng gói SqlCommand bất đồng bộ và truyền CancellationToken xuyên suốt I/O.
    private static async Task<HashSet<string>> ReadNames(SqlConnection connection, string sql, CancellationToken ct) { var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase); await using var command = new SqlCommand(sql, connection); await using var reader = await command.ExecuteReaderAsync(ct); while (await reader.ReadAsync(ct)) result.Add(reader.GetString(0)); return result; }
    private static async Task<long> Scalar(SqlConnection connection, string sql, CancellationToken ct) { await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 }; return Convert.ToInt64(await command.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture); }
    private static async Task<string?> TextScalar(SqlConnection connection, string sql, CancellationToken ct) { await using var command = new SqlCommand(sql, connection); return Convert.ToString(await command.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture); }
    private static async Task Add(SqlConnection connection, List<LegacyQualityFinding> findings, string code, string severity, string sql, string action, CancellationToken ct) { var count = await Scalar(connection, sql, ct); if (count > 0) findings.Add(new(code, severity, count, action)); }
}
