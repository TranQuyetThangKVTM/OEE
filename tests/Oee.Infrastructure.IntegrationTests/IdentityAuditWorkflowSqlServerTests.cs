using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Oee.Application.Audit;
using Oee.Application.Common;
using Oee.Application.Contracts.Audit;
using Oee.Application.Contracts.Identity;
using Oee.Application.Security;
using Oee.Domain.Platform;
using Oee.Infrastructure.Identity;
using Oee.Infrastructure.Persistence;
using Oee.Tests.Fixtures;

namespace Oee.Infrastructure.IntegrationTests;

/// <summary>
/// Integration Test end-to-end cho ASP.NET Core Identity, phân quyền và Audit append-only trên SQL Server.
/// </summary>
/// <remarks>
/// SQL Server thật được dùng để kiểm tra Identity store, transaction, concurrency token, retention và ràng buộc
/// không cho sửa/xóa AuditLog. Mật khẩu trong Arrange chỉ là dữ liệu test và phải không xuất hiện trong audit.
/// </remarks>
public sealed class IdentityAuditWorkflowSqlServerTests
{
    [SqlServerFact]
    [Trait("Category", "SqlServer")]
    public async Task IdentityWorkflowEnforcesConcurrencySelfAndLastAdminRulesAndWritesSafeAudit()
    {
        // Arrange: bỏ qua test SQL khi CI chưa cung cấp server; nếu có thì dùng database tách biệt theo GUID.
        var baseConnectionString = Environment.GetEnvironmentVariable("OEE_TEST_SQLSERVER");
        if (string.IsNullOrWhiteSpace(baseConnectionString))
        {
            return;
        }

        var databaseName = $"OeeDb_IdentityTests_{Guid.NewGuid():N}";
        var connection = new SqlConnectionStringBuilder(baseConnectionString)
        {
            InitialCatalog = databaseName
        };
        Assert.StartsWith("OeeDb_IdentityTests_", connection.InitialCatalog, StringComparison.Ordinal);
        var actor = new MutableActor();
        var clock = new FixedClock();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ICurrentActor>(actor);
        services.AddSingleton<IClock>(clock);
        services.AddScoped<ISaveChangesInterceptor, FailAdministrativeAuditInterceptor>();
        services.AddOeeInfrastructure(connection.ConnectionString);
        await using var provider = services.BuildServiceProvider();

        try
        {
            // Arrange: migrate schema Identity/Audit và seed các role từ ma trận quyền thực tế của ứng dụng.
            await using (var migrationScope = provider.CreateAsyncScope())
            {
                var db = migrationScope.ServiceProvider.GetRequiredService<OeeDbContext>();
                await db.Database.MigrateAsync();
                var roleManager = migrationScope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
                foreach (var role in RoleCapabilityMatrix.Roles)
                {
                    var result = await roleManager.CreateAsync(new IdentityRole<Guid>(role));
                    Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(error => error.Description)));
                }
            }

            await using var scope = provider.CreateAsyncScope();
            var users = new UserManagementScenario(scope.ServiceProvider.GetRequiredService<IUseCaseExecutor>());
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var dbContext = scope.ServiceProvider.GetRequiredService<OeeDbContext>();
            var auditQuery = scope.ServiceProvider.GetRequiredService<IAuditQueryService>();
            var maintenance = new AuditMaintenanceScenario(scope.ServiceProvider.GetRequiredService<IUseCaseExecutor>());

            // Identity và audit phải rollback cùng nhau; lỗi không để lại user/role hay tracking dở dang.
            await Assert.ThrowsAsync<InvalidOperationException>(() => users.CreateAsync(
                new CreateUserRequest("rollback-user", "rollback@example.test", "Rollback", "Rollback@123", [RoleCapabilityMatrix.Operator]),
                CancellationToken.None));
            Assert.False(await dbContext.Users.AnyAsync(user => user.UserName == "rollback-user"));
            Assert.False(await dbContext.AuditLogs.AnyAsync(log => log.EntitySubject == "rollback-user"));
            Assert.False(scope.ServiceProvider.GetRequiredService<IApplicationUnitOfWork>().HasChanges);

            // Act & Assert: tạo user hợp lệ, rồi kiểm tra trùng username, email, password policy và role không tồn tại.
            var created = await users.CreateAsync(
                new CreateUserRequest(
                    "operator1",
                    "operator1@example.test",
                    "Operator One",
                    "Operator@123",
                    [RoleCapabilityMatrix.Viewer]),
                CancellationToken.None);
            Assert.Equal(RoleCapabilityMatrix.Viewer, Assert.Single(created.Roles));
            Assert.False(string.IsNullOrWhiteSpace(created.ConcurrencyToken));
            Assert.Equal(RoleCapabilityMatrix.Roles.Count, (await users.RolesAsync(CancellationToken.None)).Count);
            Assert.Equal(created.UserName, (await users.GetAsync(created.Id, CancellationToken.None)).UserName);
            await Assert.ThrowsAsync<ApplicationRuleException>(() => users.CreateAsync(
                new CreateUserRequest("OPERATOR1", "other@example.test", "Duplicate", "Operator@123", [RoleCapabilityMatrix.Viewer]),
                CancellationToken.None));
            await Assert.ThrowsAsync<ApplicationRuleException>(() => users.CreateAsync(
                new CreateUserRequest("invalid-email", "not-an-email", "Invalid Email", "Operator@123", [RoleCapabilityMatrix.Viewer]),
                CancellationToken.None));
            await Assert.ThrowsAsync<ApplicationRuleException>(() => users.CreateAsync(
                new CreateUserRequest("weak-password", "weak@example.test", "Weak Password", "weak", [RoleCapabilityMatrix.Viewer]),
                CancellationToken.None));
            await Assert.ThrowsAsync<ApplicationRuleException>(() => users.CreateAsync(
                new CreateUserRequest("invalid-role", "role@example.test", "Invalid Role", "Operator@123", ["NoSuchRole"]),
                CancellationToken.None));

            // Act & Assert: cập nhật hồ sơ/role, cấm đổi username và phát hiện RowVersion cũ.
            var updated = await users.UpdateAsync(
                created.Id,
                new UpdateUserRequest(
                    "operator1",
                    "operator1@example.test",
                    "Operator Updated",
                    [RoleCapabilityMatrix.Operator],
                    created.ConcurrencyToken),
                CancellationToken.None);
            Assert.Equal("Operator Updated", updated.DisplayName);
            Assert.Equal(RoleCapabilityMatrix.Operator, Assert.Single(updated.Roles));
            Assert.NotEqual(created.ConcurrencyToken, updated.ConcurrencyToken);
            await Assert.ThrowsAsync<ApplicationRuleException>(() => users.UpdateAsync(
                created.Id,
                new UpdateUserRequest("renamed", "operator1@example.test", "Renamed", [RoleCapabilityMatrix.Operator], updated.ConcurrencyToken),
                CancellationToken.None));
            await Assert.ThrowsAsync<ConcurrencyConflictException>(() => users.UpdateAsync(
                created.Id,
                new UpdateUserRequest(
                    "operator1",
                    "operator1@example.test",
                    "Stale Update",
                    [RoleCapabilityMatrix.Operator],
                    created.ConcurrencyToken),
                CancellationToken.None));

            // Act & Assert: reset password qua UserManager, mật khẩu cũ hết hiệu lực và số lần đăng nhập sai về 0.
            await users.ResetPasswordAsync(
                created.Id,
                new ResetUserPasswordRequest("Changed@123", updated.ConcurrencyToken),
                CancellationToken.None);
            var identityUser = await userManager.FindByIdAsync(created.Id.ToString());
            Assert.NotNull(identityUser);
            Assert.True(await userManager.CheckPasswordAsync(identityUser, "Changed@123"));
            Assert.False(await userManager.CheckPasswordAsync(identityUser, "Operator@123"));
            Assert.Equal(0, identityUser.AccessFailedCount);

            // Act & Assert: đổi mật khẩu của chính người đang đăng nhập.
            var refreshed = await SearchSingleAsync(users, "operator1");
            actor.UserId = refreshed.Id.ToString();
            await users.ChangeOwnPasswordAsync(
                new ChangeOwnPasswordRequest("Changed@123", "OwnChange@123"),
                CancellationToken.None);
            identityUser = await userManager.FindByIdAsync(created.Id.ToString());
            Assert.NotNull(identityUser);
            Assert.True(await userManager.CheckPasswordAsync(identityUser, "OwnChange@123"));
            refreshed = await SearchSingleAsync(users, "operator1");
            await Assert.ThrowsAsync<ApplicationRuleException>(() => users.SetRolesAsync(
                refreshed.Id,
                new SetUserRolesRequest([RoleCapabilityMatrix.Admin], refreshed.ConcurrencyToken),
                CancellationToken.None));

            // Act & Assert: không cho tự nâng quyền và không cho vô hiệu hóa Admin cuối cùng.
            actor.UserId = Guid.Empty.ToString();
            var onlyAdmin = await users.CreateAsync(
                new CreateUserRequest(
                    "onlyadmin",
                    "onlyadmin@example.test",
                    "Only Admin",
                    "AdminOnly@123",
                    [RoleCapabilityMatrix.Admin]),
                CancellationToken.None);
            await Assert.ThrowsAsync<ApplicationRuleException>(() => users.DisableAsync(
                onlyAdmin.Id,
                onlyAdmin.ConcurrencyToken,
                CancellationToken.None));

            refreshed = await SearchSingleAsync(users, "operator1");
            await users.DisableAsync(refreshed.Id, refreshed.ConcurrencyToken, CancellationToken.None);
            refreshed = await SearchSingleAsync(users, "operator1");
            Assert.True(refreshed.IsDisabled);

            // Assert: mọi mutation quan trọng có semantic audit nhưng không lưu mật khẩu hoặc PasswordHash.
            var semanticAudits = await dbContext.AuditLogs.AsNoTracking()
                .Where(log => log.EntityType == nameof(ApplicationUser))
                .ToArrayAsync();
            Assert.Contains(semanticAudits, log => log.Action == "UserCreated");
            Assert.Contains(semanticAudits, log => log.Action == "UserUpdated");
            Assert.Contains(semanticAudits, log => log.Action == "UserPasswordReset");
            Assert.Contains(semanticAudits, log => log.Action == "UserDisabled");
            Assert.Contains(semanticAudits, log => log.Action == "OwnPasswordChanged");
            Assert.All(semanticAudits, log =>
            {
                Assert.DoesNotContain("PasswordHash", log.Changes ?? string.Empty, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Changed@123", log.Changes ?? string.Empty, StringComparison.Ordinal);
                Assert.DoesNotContain("Operator@123", log.Changes ?? string.Empty, StringComparison.Ordinal);
            });

            // Act & Assert: AuditRead xem bản thân thiện; chỉ AuditRawJsonRead mới nhận JSON gốc đã masking.
            var friendlyOnlyQuery = new Oee.Infrastructure.Audit.AuditQueryService(
                dbContext, new CapabilitySet(Capabilities.AuditRead));
            var friendlyOnly = await friendlyOnlyQuery.DetailAsync(semanticAudits.First().Id, CancellationToken.None);
            Assert.Null(friendlyOnly.RawJson);
            Assert.DoesNotContain(friendlyOnly.Fields, field =>
                field.PropertyName.Contains("Password", StringComparison.OrdinalIgnoreCase));
            var rawQuery = new Oee.Infrastructure.Audit.AuditQueryService(
                dbContext, new CapabilitySet(Capabilities.AuditRead, Capabilities.AuditRawJsonRead));
            var raw = await rawQuery.DetailAsync(semanticAudits.First().Id, CancellationToken.None);
            Assert.NotNull(raw.RawJson);
            Assert.DoesNotContain("PasswordHash", raw.RawJson, StringComparison.OrdinalIgnoreCase);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                new Oee.Infrastructure.Audit.AuditQueryService(dbContext, new CapabilitySet())
                    .SearchAsync(new AuditLogSearchRequest(), CancellationToken.None));
            // User read được xác thực ở Web; chỉ mutation yêu cầu capability riêng.
            var readOnlyUsers = new Oee.Tests.UseCases.UserManagementScenario(
                new IdentityAccountGateway(userManager, scope.ServiceProvider.GetRequiredService<EfApplicationUnitOfWork>()),
                scope.ServiceProvider.GetRequiredService<IApplicationUnitOfWork>(), actor, clock, new CapabilitySet());
            Assert.True((await readOnlyUsers.SearchAsync(new UserSearchRequest(), CancellationToken.None)).TotalCount > 0);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                readOnlyUsers.DisableAsync(created.Id, created.ConcurrencyToken, CancellationToken.None));

            // Act & Assert: guard trong DbContext bảo vệ tính append-only, không cho xóa từng audit entry.
            var protectedAudit = semanticAudits.First();
            dbContext.ChangeTracker.Clear();
            dbContext.AuditLogs.Remove(protectedAudit);
            await Assert.ThrowsAsync<InvalidOperationException>(() => dbContext.SaveChangesAsync());
            dbContext.ChangeTracker.Clear();

            // Act & Assert: tìm kiếm audit chạy server-side và trả đúng tổng số theo filter.
            var filtered = await auditQuery.SearchAsync(
                new AuditLogSearchRequest(
                    User: "identity-test",
                    Action: "UserUpdated",
                    SortBy: "action",
                    Descending: false,
                    Page: 1,
                    PageSize: 3),
                CancellationToken.None);
            Assert.Equal(1, filtered.TotalCount);
            Assert.Single(filtered.Items);

            // Arrange & Act: thêm một log cũ để kiểm tra retention theo cutoff cấu hình.
            dbContext.AuditLogs.Add(DomainTestData.Create<AuditLogEntity>(("Timestamp", clock.UtcNow.AddMonths(-24)), ("UserName", "legacy"), ("Action", "OldAudit"), ("EntityType", "Test"), ("EntityId", "old")));
            await dbContext.SaveChangesAsync();
            var retention = await maintenance.RunRetentionAsync(new AuditRetentionRequest(), CancellationToken.None);
            // Assert: chỉ log đủ tuổi bị dọn và dấu vết quản trị retention được lưu ở nguồn riêng.
            Assert.True(retention.DeletedCount >= 1);
            Assert.False(await dbContext.AuditLogs.AnyAsync(log => log.Action == "OldAudit"));
            Assert.True(await dbContext.AuditLogs.AnyAsync(log => log.Action == "RetentionCleanup"));
            Assert.True(await dbContext.AuditRetentionRuns.AnyAsync(run =>
                run.CandidateCount == retention.CandidateCount && run.DeletedCount == retention.DeletedCount));
            Assert.Equal(retention.CandidateCount, retention.DeletedCount);
        }
        finally
        {
            // Cleanup database test tách biệt, không xóa OeeDb development.
            await using var cleanupScope = provider.CreateAsyncScope();
            var db = cleanupScope.ServiceProvider.GetRequiredService<OeeDbContext>();
            await db.Database.EnsureDeletedAsync();
            SqlConnection.ClearAllPools();
        }
    }

    private static async Task<UserResponse> SearchSingleAsync(
        UserManagementScenario users,
        string search)
        // Helper luôn đọc lại qua Application Service để tránh dựa vào entity đang tracking.
        => Assert.Single((await users.SearchAsync(
            new UserSearchRequest(Search: search, PageSize: 10),
            CancellationToken.None)).Items);

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 8, 12, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class FailAdministrativeAuditInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context!.ChangeTracker.Entries<AuditLogEntity>().Any(entry => entry.Entity.EntitySubject == "rollback-user"))
                throw new InvalidOperationException("Injected administrative audit failure.");
            return ValueTask.FromResult(result);
        }
    }

    private sealed class MutableActor : ICurrentActor
    {
        public string? UserId { get; set; } = Guid.Empty.ToString();
        public string UserName => "identity-test";
        public string? IpAddress => "::ffff:127.0.0.1";
    }

    private sealed class CapabilitySet(params string[] capabilities) : ICapabilityChecker
    {
        public bool HasCapability(string capability) => capabilities.Contains(capability, StringComparer.Ordinal);
    }
}
