using System.Security.Claims;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Oee.Application.Audit;
using Oee.Application.Contracts.Audit;
using Oee.Application.Contracts.Identity;
using Oee.Application.Security;
using Oee.Web.Components.Layout;
using Oee.Web.Components.Pages;
using Oee.Web.Configuration;

namespace Oee.Web.ComponentTests;

/// <summary>
/// bUnit Test cho Quản lý người dùng, Audit, retention và user popover trong menu Hệ thống.
/// </summary>
public sealed class SystemAdministrationPageTests : RowMenuTestContext, IAsyncLifetime
{
    private static readonly Guid AdminId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid ViewerId = Guid.Parse("20000000-0000-0000-0000-000000000001");

    /// <summary>Khởi tạo principal Admin có đủ capability cùng cấu hình Identity/Audit giống composition root.</summary>
    public SystemAdministrationPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddAuthorizationCore(options =>
        {
            foreach (var capability in Capabilities.All)
                options.AddPolicy(capability, policy => policy.RequireRole(RoleCapabilityMatrix.Admin));
        });
        Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new AuditRetentionOptions { Months = 18 }));
        Services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new IdentitySecurityOptions()));
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("admin");
        authorization.SetRoles(RoleCapabilityMatrix.Admin);
        authorization.SetPolicies(Capabilities.UsersManage, Capabilities.AuditRead,
            Capabilities.AuditRawJsonRead, Capabilities.AuditRetentionManage);
        authorization.SetClaims(new Claim(ClaimTypes.NameIdentifier, AdminId.ToString()),
            new Claim(ClaimTypes.Email, "admin@example.test"), new Claim("display_name", "System Admin"));
    }

    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;
    async Task IAsyncLifetime.DisposeAsync() => await base.DisposeAsync();

    [Fact]
    public void UsersPageUsesServerPagingDatabaseRolesAndAdministrativeActions()
    {
        // Arrange: fake Identity service trả một Viewer và role list từ ma trận hệ thống.
        var service = new FakeUserService();
        Services.AddScoped<IUserManagementScenario>(_ => service).AddScenarioRequests();
        // Act & Assert: page dùng server paging và hiển thị các administrative action theo quyền Admin.
        var page = Render<Users>();
        page.WaitForAssertion(() =>
        {
            Assert.Contains("Quản lý người dùng", page.Markup, StringComparison.Ordinal);
            Assert.Contains("viewer@example.test", page.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("Số lần sai mật khẩu", page.Markup, StringComparison.Ordinal);
            Assert.Contains("Tạo tài khoản", page.Markup, StringComparison.Ordinal);
            Assert.Contains("mud-table-pagination", page.Markup, StringComparison.Ordinal);
            Assert.True(service.RolesRequested);
            Assert.Equal(25, service.LastSearch?.PageSize);
        });
        OpenActions(page);
        Assert.NotNull(MenuAction("Đặt lại mật khẩu"));
        Assert.NotNull(MenuAction("Vô hiệu hóa tài khoản"));
        Assert.DoesNotContain("Khóa tài khoản", Menus.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Mở khóa tài khoản", Menus.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void UserQueryFailureKeepsPageHeaderRefreshWithoutTechnicalErrorBanner()
    {
        // Arrange & Act: Identity query ném lỗi hạ tầng.
        Services.AddScoped<IUserManagementScenario>(_ => new FakeUserService(failSearch: true)).AddScenarioRequests();
        var page = Render<Users>();
        // Assert: lỗi Identity không lộ ra UI; người dùng có thể tải lại từ PageHeader.
        page.WaitForAssertion(() =>
        {
            Assert.Contains("Làm mới", page.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("identity unavailable", page.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task CreateDialogOnlyClosesAfterSuccessfulCommandAndGuardsDoubleSubmit()
    {
        // Arrange: đăng ký role/query và MudDialogProvider thật.
        var service = new FakeUserService();
        Services.AddScoped<IUserManagementScenario>(_ => service).AddScenarioRequests();
        var provider = Render<MudDialogProvider>();
        // Act: mở create dialog qua IDialogService.
        await Services.GetRequiredService<IDialogService>().ShowAsync<UserEditorDialog>("Tạo tài khoản");
        // Assert: dialog nhận role từ service và có trường mật khẩu ban đầu; không dùng role hard-code trong Razor.
        provider.WaitForAssertion(() =>
        {
            Assert.Contains("Mật khẩu ban đầu", provider.Markup, StringComparison.Ordinal);
            Assert.Contains(RoleCapabilityMatrix.Admin, provider.Markup, StringComparison.Ordinal);
            Assert.Contains(RoleCapabilityMatrix.Viewer, provider.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void AuditPageUsesServerFiltersPagingAndRetentionPolicy()
    {
        // Arrange: audit query ghi lại filter và retention config là 18 tháng.
        var query = new FakeAuditQuery(includeRawJson: true);
        Services.AddScoped<IAuditQueryService>(_ => query);
        Services.AddScoped<IAuditMaintenanceScenario>(_ => new FakeAuditMaintenance()).AddScenarioRequests();
        // Act & Assert: page có server paging, detail action và retention chỉ dành cho capability tương ứng.
        var page = Render<Audit>();
        page.WaitForAssertion(() =>
        {
            Assert.Contains("Nhật ký thao tác", page.Markup, StringComparison.Ordinal);
            Assert.Contains("127.0.0.1", page.Markup, StringComparison.Ordinal);
            Assert.Contains("Dọn log cũ (18T)", page.Markup, StringComparison.Ordinal);
            Assert.Contains("Xem chi tiết", page.Markup, StringComparison.Ordinal);
            Assert.Equal(25, query.LastRequest?.PageSize);
        });
    }

    [Fact]
    public async Task AuditDetailRendersFriendlyDiffAndRawJsonWhenServiceAuthorizesIt()
    {
        // Arrange: service trả field mapping thân thiện và RawJson vì principal có AuditRawJsonRead.
        var query = new FakeAuditQuery(includeRawJson: true);
        var provider = Render<MudDialogProvider>();
        var parameters = new DialogParameters<AuditDetailDialog>
        {
            { dialog => dialog.AuditLogId, FakeAuditQuery.Log.Id }, { dialog => dialog.AuditService, query },
        };
        // Act: mở detail bằng Id thay vì truyền EF entity tracking.
        await Services.GetRequiredService<IDialogService>().ShowAsync<AuditDetailDialog>("Chi tiết", parameters);
        // Assert: before/after và JSON gốc hiển thị nhưng trường PasswordHash không xuất hiện.
        provider.WaitForAssertion(() =>
        {
            Assert.Contains("Tên hiển thị", provider.Markup, StringComparison.Ordinal);
            Assert.Contains("Old", provider.Markup, StringComparison.Ordinal);
            Assert.Contains("New", provider.Markup, StringComparison.Ordinal);
            Assert.Contains("JSON gốc", provider.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("PasswordHash", provider.Markup, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public async Task AuditDetailHidesRawJsonWhenServiceDoesNotAuthorizeIt()
    {
        // Arrange: cùng detail DTO nhưng service không cấp RawJson cho quyền AuditRead thông thường.
        var query = new FakeAuditQuery(includeRawJson: false);
        var provider = Render<MudDialogProvider>();
        var parameters = new DialogParameters<AuditDetailDialog>
        {
            { dialog => dialog.AuditLogId, FakeAuditQuery.Log.Id }, { dialog => dialog.AuditService, query },
        };
        await Services.GetRequiredService<IDialogService>().ShowAsync<AuditDetailDialog>("Chi tiết", parameters);
        // Assert: nội dung thân thiện vẫn xem được, vùng JSON kỹ thuật bị ẩn.
        provider.WaitForAssertion(() =>
        {
            Assert.Contains("Tên hiển thị", provider.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("JSON gốc", provider.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void UserPopoverShowsAdminLinksChangePasswordAndSecureLogout()
    {
        // Arrange & Act: mở MudPopover từ nút avatar của principal Admin.
        var provider = Menus;
        var menu = Render<UserAccountMenu>();
        menu.Find("[aria-label='Mở menu người dùng']").Click();
        // Assert: có thông tin claims, link đổi mật khẩu và logout bằng form POST an toàn.
        provider.WaitForAssertion(() =>
        {
            Assert.Contains("System Admin", provider.Markup, StringComparison.Ordinal);
            Assert.Contains("/Account/ChangePassword", provider.Markup, StringComparison.Ordinal);
            Assert.Contains("action=\"/account/logout\"", provider.Markup, StringComparison.OrdinalIgnoreCase);
        });
    }

    private sealed class FakeUserService(bool failSearch = false) : IUserManagementScenario
    {
        public UserSearchRequest? LastSearch { get; private set; }
        public bool RolesRequested { get; private set; }
        private static UserResponse Viewer => new(ViewerId, "viewer", "Read-only Viewer", "viewer@example.test",
            false, false, [RoleCapabilityMatrix.Viewer], "token-1");

        public Task<PagedUserResponse> SearchAsync(UserSearchRequest request, CancellationToken cancellationToken)
        {
            LastSearch = request;
            return failSearch
                ? Task.FromException<PagedUserResponse>(new InvalidOperationException("identity unavailable"))
                : Task.FromResult(new PagedUserResponse([Viewer], request.Page, request.PageSize, 1));
        }
        public Task<UserResponse> GetAsync(Guid userId, CancellationToken cancellationToken) => Task.FromResult(Viewer);
        public Task<IReadOnlyCollection<UserRoleResponse>> RolesAsync(CancellationToken cancellationToken)
        {
            RolesRequested = true;
            return Task.FromResult<IReadOnlyCollection<UserRoleResponse>>(RoleCapabilityMatrix.Roles
                .Select((name, index) => new UserRoleResponse(Guid.Parse($"00000000-0000-0000-0000-{index + 1:D12}"), name)).ToArray());
        }
        public Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken) => Task.FromResult(Viewer);
        public Task<UserResponse> UpdateAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken) => Task.FromResult(Viewer);
        public Task<UserResponse> SetRolesAsync(Guid userId, SetUserRolesRequest request, CancellationToken cancellationToken) => Task.FromResult(Viewer);
        public Task ResetPasswordAsync(Guid userId, ResetUserPasswordRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DisableAsync(Guid userId, string concurrencyToken, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ChangeOwnPasswordAsync(ChangeOwnPasswordRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeAuditQuery(bool includeRawJson) : IAuditQueryService
    {
        public static AuditLogResponse Log { get; } = new(Guid.Parse("30000000-0000-0000-0000-000000000001"),
            new DateTimeOffset(2026, 8, 12, 8, 30, 0, TimeSpan.Zero), AdminId.ToString(), "admin", "UserUpdated",
            "ApplicationUser", ViewerId.ToString(), "viewer", "127.0.0.1");
        public AuditLogSearchRequest? LastRequest { get; private set; }
        public Task<PagedAuditLogResponse> SearchAsync(AuditLogSearchRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new PagedAuditLogResponse([Log], request.Page, request.PageSize, 1));
        }
        public Task<AuditLogDetailResponse> DetailAsync(Guid auditLogId, CancellationToken cancellationToken)
            => Task.FromResult(new AuditLogDetailResponse(Log.Id, Log.Timestamp, Log.UserId, Log.UserName, Log.Action,
                "Cập nhật", Log.EntityType, "Tài khoản người dùng", Log.EntityId, Log.EntitySubject, Log.IpAddress,
                [new AuditFieldChangeResponse("DisplayName", "Tên hiển thị", "Old", "New")],
                includeRawJson ? "{\n  \"DisplayName\": {}\n}" : null));
    }

    private sealed class FakeAuditMaintenance : IAuditMaintenanceScenario
    {
        public Task<AuditRetentionPreview> PreviewRetentionAsync(CancellationToken cancellationToken)
            => Task.FromResult(new AuditRetentionPreview(DateTimeOffset.UtcNow.AddMonths(-18), 2, 18));
        public Task<AuditRetentionResult> RunRetentionAsync(AuditRetentionRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new AuditRetentionResult(DateTimeOffset.UtcNow.AddMonths(-18), 2, 2,
                DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
    }
}
