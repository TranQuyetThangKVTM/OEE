namespace Oee.Infrastructure.IntegrationTests;

/// <summary>Hiển thị Skipped khi chưa cấu hình SQL test; không biến test chưa chạy thành Passed.</summary>
public sealed class SqlServerFactAttribute : FactAttribute
{
    internal static string? ConfigurationSkipReason =>
        string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OEE_TEST_SQLSERVER"))
            ? "Chưa cấu hình OEE_TEST_SQLSERVER; cần SQL Server và database test riêng."
            : null;

    public SqlServerFactAttribute() => Skip = ConfigurationSkipReason;
}
