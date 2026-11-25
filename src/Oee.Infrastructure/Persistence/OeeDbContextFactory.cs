using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Oee.Infrastructure.Persistence;

/// <summary>
/// Factory chỉ dùng lúc chạy lệnh <c>dotnet ef</c> để tạo <see cref="OeeDbContext"/> ngoài Web/Worker.
/// </summary>
/// <remarks>
/// Connection string bắt buộc đến từ environment variable; factory không hard-code credential và
/// không tự tạo/cập nhật database. Migration chỉ chạy khi người vận hành gọi công cụ kiểm soát.
/// </remarks>
public sealed class OeeDbContextFactory : IDesignTimeDbContextFactory<OeeDbContext>
{
    private const string ConnectionStringEnvironmentVariable = "ConnectionStrings__DefaultConnection";

    public OeeDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Environment variable {ConnectionStringEnvironmentVariable} is required for EF design-time operations.");
        }

        var options = new DbContextOptionsBuilder<OeeDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new OeeDbContext(options);
    }
}
