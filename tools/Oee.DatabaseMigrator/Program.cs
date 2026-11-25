using System.Globalization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Oee.Application.PlcIntegration;
using Oee.Application.Security;
using Oee.DatabaseMigrator;
using Oee.Infrastructure.Audit;
using Oee.Infrastructure.Identity;
using Oee.Infrastructure.Persistence;
using Oee.Infrastructure.Security;
using Serilog;

// Bước 1: chỉ chấp nhận đúng một command trong allow-list; công cụ không chạy migration ngầm khi thiếu ý định rõ ràng.
var supportedCommands = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "migrate", "seed", "verify", "all", "reset-admin-password", "legacy-preflight", "legacy-dry-run", "export-master", "initialize-fieldtest" };
if (args.Length != 1 || !supportedCommands.Contains(args[0]))
{
    Console.Error.WriteLine("Commands: migrate|seed|verify|all|reset-admin-password|legacy-preflight|legacy-dry-run|export-master|initialize-fieldtest");
    return 2;
}

// Bước 2: Generic Host nạp appsettings, environment/User Secrets, logging và Dependency Injection giống các host .NET khác.
var builder = Host.CreateApplicationBuilder(args);
var settingsFile = Environment.GetEnvironmentVariable("OEE_SETTINGS_FILE");
if (!string.IsNullOrWhiteSpace(settingsFile))
    builder.Configuration.AddJsonFile(settingsFile, optional: false).AddEnvironmentVariables();
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("ConnectionStrings__DefaultConnection is required through development settings, an environment variable, or User Secrets.");
    return 3;
}

// Guard catalog đích giúp lệnh DDL/seed của công cụ development không chạy nhầm một database tên khác.
var sqlConnection = new SqlConnectionStringBuilder(connectionString);
var expectedDatabase = builder.Configuration["Deployment:ExpectedDatabase"] ?? "OeeDb";
if (expectedDatabase is not ("OeeDb" or "OeeMasan2")
    || !string.Equals(sqlConnection.InitialCatalog, expectedDatabase, StringComparison.Ordinal))
{
    Console.Error.WriteLine("Safety check failed: catalog must match Deployment:ExpectedDatabase (OeeDb or OeeMasan2).");
    return 4;
}

// Bước 3: công cụ chỉ cần persistence, audit và Identity store cho migrate/seed.
// Không đăng ký dịch vụ PLC hoặc SignInManager của Web: Development kiểm tra đầy đủ dependency lúc Build.
builder.Services.AddSerilog((services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture));
builder.Services.AddScoped<ICurrentActor, SystemCurrentActor>();
builder.Services.AddScoped<AuditSaveChangesInterceptor>();
builder.Services.AddDbContext<OeeDbContext>((provider, options) => options
    .UseSqlServer(connectionString)
    .AddInterceptors(provider.GetRequiredService<AuditSaveChangesInterceptor>()));
builder.Services.AddDataProtection();
builder.Services.AddIdentityCore<ApplicationUser>()
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<OeeDbContext>()
    .AddDefaultTokenProviders();
builder.Services.Configure<SeedOptions>(builder.Configuration.GetSection(SeedOptions.SectionName));
builder.Services.AddScoped<DatabaseSeeder>();

// Bước 4: tạo một DI scope duy nhất cho command, sau đó giải phóng DbContext/connection khi tiến trình kết thúc.
using var host = builder.Build();
await using var scope = host.Services.CreateAsyncScope();
var services = scope.ServiceProvider;
var dbContext = services.GetRequiredService<OeeDbContext>();
var command = args[0];

try
{
    // Nhánh legacy hiện chỉ preflight/dry-run nguồn ở chế độ read-only; connection nguồn tách khỏi OeeDb đích.
    if (command.StartsWith("legacy-", StringComparison.OrdinalIgnoreCase))
    {
        var source = builder.Configuration.GetConnectionString("LegacySource");
        if (string.IsNullOrWhiteSpace(source)) { Console.Error.WriteLine("ConnectionStrings__LegacySource is required. Do not place production credentials in appsettings."); return 20; }
        // Backup confirmation là tín hiệu quy trình bên ngoài, không thay thế việc tạo/kiểm chứng bản backup thật.
        var backupConfirmed = string.Equals(Environment.GetEnvironmentVariable("OEE_LEGACY_BACKUP_CONFIRMED"), "YES", StringComparison.Ordinal);
        var report = await new LegacyPreflightService(source).RunAsync(backupConfirmed, CancellationToken.None);
        Console.WriteLine(LegacyPreflightService.Serialize(report));
        if (!report.SchemaCompatible || report.Findings.Any(x => x.Severity == "Critical")) return 21;
        if (command.Equals("legacy-dry-run", StringComparison.OrdinalIgnoreCase) && !backupConfirmed) { Console.Error.WriteLine("Dry-run passed, but apply remains blocked until OEE_LEGACY_BACKUP_CONFIRMED=YES and backup evidence is recorded."); return 22; }
        return 0;
    }
    // Trước khi chạm database đích, probe catalog master để báo rõ lỗi network/login SQL Server.
    var probeConnectionString = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" };
    await using (var probeConnection = new SqlConnection(probeConnectionString.ConnectionString))
    {
        await probeConnection.OpenAsync();
        Console.WriteLine($"SQL Server connection and login succeeded; target catalog guard is {expectedDatabase}.");
    }

    var masterDataFile = builder.Configuration["Deployment:MasterDataFile"];
    if (command.Equals("export-master", StringComparison.OrdinalIgnoreCase))
    {
        if (string.IsNullOrWhiteSpace(masterDataFile)) throw new InvalidOperationException("Deployment:MasterDataFile is required.");
        await new MasterDataTransfer(dbContext).ExportAsync(Path.GetFullPath(masterDataFile), CancellationToken.None);
        Console.WriteLine("Master-data snapshot exported; no Identity/runtime/history rows included.");
        return 0;
    }
    if (command.Equals("initialize-fieldtest", StringComparison.OrdinalIgnoreCase))
    {
        if (expectedDatabase != "OeeMasan2") throw new InvalidOperationException("Field-test initialization requires OeeMasan2.");
        if (string.IsNullOrWhiteSpace(masterDataFile) || !File.Exists(masterDataFile))
            throw new InvalidOperationException("Deployment:MasterDataFile must reference the package snapshot.");
        if (string.IsNullOrWhiteSpace(builder.Configuration["Seed:AdminPassword"]))
            throw new InvalidOperationException("Provide Seed__AdminPassword before initialization.");
        // Chỉ khởi tạo database chưa tồn tại; không migrate/import đè một database đang dùng.
        await using var probe = new SqlConnection(new SqlConnectionStringBuilder(connectionString) { InitialCatalog = "master" }.ConnectionString);
        await probe.OpenAsync();
        await using var exists = probe.CreateCommand();
        exists.CommandText = "SELECT COUNT(*) FROM sys.databases WHERE name = @name";
        exists.Parameters.AddWithValue("@name", expectedDatabase);
        if (Convert.ToInt32(await exists.ExecuteScalarAsync(), CultureInfo.InvariantCulture) != 0)
            throw new InvalidOperationException("OeeMasan2 already exists. Initialization stopped without modifying it; use verify.");
        await services.GetRequiredService<DatabaseSeeder>().ValidateAdministratorPasswordAsync();
        await dbContext.Database.MigrateAsync();
        var transfer = new MasterDataTransfer(dbContext);
        await transfer.ImportAsync(Path.GetFullPath(masterDataFile), CancellationToken.None);
        await services.GetRequiredService<DatabaseSeeder>().SeedAdministratorAsync();
        await transfer.InitializeOpcUaAsync(builder.Configuration.GetSection("OpcUa").Get<OpcUaClientOptions>() ?? new(), CancellationToken.None);
        Console.WriteLine("OeeMasan2 created, current master data imported, administrator and OPC UA connection initialized.");
        return 0;
    }

    // migrate/all là hai command duy nhất áp dụng các migration còn pending bằng Database.MigrateAsync.
    if (command.Equals("migrate", StringComparison.OrdinalIgnoreCase)
        || command.Equals("all", StringComparison.OrdinalIgnoreCase))
    {
        await dbContext.Database.MigrateAsync();
        Console.WriteLine("Database migration completed successfully.");
    }

    // seed/all chạy idempotent seed; mật khẩu Admin lấy từ Options đã bind bằng secret.
    if (command.Equals("seed", StringComparison.OrdinalIgnoreCase)
        || command.Equals("all", StringComparison.OrdinalIgnoreCase))
    {
        await services.GetRequiredService<DatabaseSeeder>().SeedAsync(CancellationToken.None);
        Console.WriteLine("Idempotent role, administrator, and master-data seed completed successfully.");
    }

    // Reset password là thao tác development tách biệt, dùng UserManager và không in mật khẩu ra console.
    if (command.Equals("reset-admin-password", StringComparison.OrdinalIgnoreCase))
    {
        await services.GetRequiredService<DatabaseSeeder>().ResetAdminPasswordAsync();
        Console.WriteLine("Development administrator password reset and account unlock completed successfully.");
    }

    // verify/all chỉ đọc trạng thái migration và các count tổng hợp, không in hash/secret/PII của từng bản ghi.
    if (command.Equals("verify", StringComparison.OrdinalIgnoreCase)
        || command.Equals("all", StringComparison.OrdinalIgnoreCase))
    {
        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
        Console.WriteLine($"Pending migrations: {pendingMigrations.Count()}.");
        Console.WriteLine($"Tables represented by EF model: {dbContext.Model.GetEntityTypes().Select(type => type.GetTableName()).Where(name => name is not null).Distinct(StringComparer.Ordinal).Count()}.");
        Console.WriteLine($"Seed counts - Roles: {await dbContext.Roles.CountAsync()}, Users: {await dbContext.Users.CountAsync()}, Sites: {await dbContext.Sites.CountAsync()}, WorkUnits: {await dbContext.WorkUnits.CountAsync()}, Products: {await dbContext.Products.CountAsync()}, Shifts: {await dbContext.Shifts.CountAsync()}, Standards: {await dbContext.WorkUnitStandards.CountAsync()}, DowntimeReasons: {await dbContext.DowntimeReasons.CountAsync()}, Targets: {await dbContext.OeeTargets.CountAsync()}.");
    }

    return 0;
}
catch (SqlException exception)
{
    // Trả exit code riêng cho lỗi SQL để script triển khai phân biệt với lỗi usage/safety/preflight.
    Console.Error.WriteLine($"SQL Server operation failed (Number {exception.Number}, State {exception.State}): {exception.Message}");
    return 10;
}
