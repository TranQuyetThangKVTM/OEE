using System.Net;
using System.Net.Http.Json;

namespace Oee.Infrastructure.IntegrationTests;

/// <summary>
/// Integration Test khởi động Oee.Web trong bộ nhớ để bảo vệ route, authentication boundary và OpenAPI.
/// </summary>
[Collection("Read endpoint host")]
public sealed class ApiBoundaryTests : IDisposable
{
    private readonly HttpClient client;

    /// <summary>
    /// Tạo HTTP client không tự đi theo redirect để test phân biệt chính xác 302 của page và 401 của API.
    /// </summary>
    public ApiBoundaryTests(ReadEndpointHostFixture fixture)
    {
        client = fixture.CreateClient();
    }

    public void Dispose() => client.Dispose();

    [Fact]
    public async Task IisLivenessWarmupWorksWithoutLoginOrDatabaseAccess()
    {
        using var response = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task ProtectedMobileEndpointReturns401InsteadOfHtmlRedirect()
    {
        // Act: gọi API bảo vệ khi chưa đăng nhập.
        using var response = await client.GetAsync("/api/v1/production/status");
        // Assert: API trả 401 thuần, không chuyển mobile client sang trang HTML đăng nhập.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }


    [Fact]
    public async Task RemovedManualProductionEndpointCannotBePosted()
    {
        using var response = await client.PostAsync("/api/v1/production-outputs/manual", new FormUrlEncodedContent([]));
        Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task DevelopmentOpenApiPublishesV1ButNotSignalRTransport()
    {
        // Act: tải tài liệu OpenAPI của môi trường Development.
        var document = await client.GetStringAsync("/openapi/v1.json");
        // Assert: API v1 được công bố còn transport nội bộ SignalR không bị mô tả như REST API.
        Assert.Contains("/api/v1/production/status", document, StringComparison.Ordinal);
        Assert.Contains("/api/v1/master/lookups/{kind}", document, StringComparison.Ordinal);
        Assert.DoesNotContain("/hubs/oee-live", document, StringComparison.Ordinal);
        Assert.DoesNotContain("/production-reconciliation/", document, StringComparison.Ordinal);
        Assert.DoesNotContain("/system/status", document, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/v1/production/outputs", document, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/v1/production-outputs/manual", document, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LoginRouteExistsAndContainsSecureForm()
    {
        // Act: tải trang login và gửi một form rỗng để đi qua antiforgery/validation pipeline.
        using var response = await client.GetAsync("/Account/Login?ReturnUrl=%2F");
        // Assert: form có token chống CSRF và thuộc tính trình duyệt phù hợp cho mật khẩu.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("action=\"/account/sign-in\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"__RequestVerificationToken\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("autocomplete=\"current-password\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("name=\"rememberMe\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("class=\"login-shell\"", html, StringComparison.OrdinalIgnoreCase);

        using var invalidPost = await client.PostAsync("/account/sign-in", new FormUrlEncodedContent([]));
        Assert.NotEqual(HttpStatusCode.InternalServerError, invalidPost.StatusCode);
    }

    [Fact]
    public async Task AnonymousDashboardRedirectsToLogin()
    {
        // Act: người chưa xác thực mở dashboard Blazor.
        using var response = await client.GetAsync("/");

        // Assert: page dùng cookie flow nên redirect an toàn kèm ReturnUrl nội bộ.
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.StartsWith("http://localhost/Account/Login", response.Headers.Location.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ReturnUrl=%2F", response.Headers.Location.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/production-plans")]
    [InlineData("/production/plan-templates")]
    [InlineData("/downtime")]
    [InlineData("/master/products")]
    [InlineData("/master/work-unit-standards")]
    [InlineData("/master/shifts")]
    [InlineData("/master/downtime-reasons")]
    [InlineData("/equipment/hierarchy")]
    [InlineData("/plc/tag-monitor")]
    [InlineData("/reports/device-detail")]
    [InlineData("/reports/shift-kpi")]
    [InlineData("/reports/daily-oee")]
    [InlineData("/reports/weekly-kpi")]
    [InlineData("/reports/monthly-kpi")]
    [InlineData("/reports/downtime-events")]
    [InlineData("/reports/quality-loss")]
    [InlineData("/reports/reliability")]
    [InlineData("/alerts")]
    [InlineData("/alerts/targets")]
    [InlineData("/live-oee")]
    [InlineData("/integration/plc-live")]
    [InlineData("/users")]
    [InlineData("/account/users")]
    [InlineData("/audit")]
    [InlineData("/account/audit-log")]
    [InlineData("/Account/ChangePassword")]
    public async Task AnonymousPagesRedirectToLogin(string route)
    {
        using var response = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.StartsWith("http://localhost/Account/Login", response.Headers.Location.ToString(), StringComparison.OrdinalIgnoreCase);
    }


    [Theory]
    [InlineData("/health-ui")]
    [InlineData("/production/reconciliation")]
    [InlineData("/api/v1/system/status")]
    [InlineData("/equipment/enterprises")]
    [InlineData("/equipment/sites")]
    [InlineData("/master/areas")]
    [InlineData("/master/processes")]
    [InlineData("/master/work-centers")]
    [InlineData("/master/work-unit-groups")]
    [InlineData("/master/work-units")]
    [InlineData("/reports")]
    [InlineData("/reports/oee")]
    [InlineData("/reports/oee-analytics")]
    [InlineData("/reports/snapshots")]
    [InlineData("/reports/plan-vs-actual")]
    [InlineData("/reports/downtime-pareto")]
    [InlineData("/production/status")]
    [InlineData("/production-status")]
    [InlineData("/production-output")]
    [InlineData("/production/output-list")]
    [InlineData("/production/outputs/new")]
    [InlineData("/production/outputs/11111111-1111-1111-1111-111111111111")]
    [InlineData("/api/v1/production/outputs")]
    public async Task RemovedPageRoutesReturnNotFound(string route)
    {
        using var response = await client.GetAsync(route);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }




    [Theory]
    [InlineData("/api/v1/users?page=1&pageSize=25&descending=false")]
    [InlineData("/api/v1/audit?page=1&pageSize=25&descending=true")]
    public async Task AnonymousIdentityAndAuditApisReturn401(string route)
    {
        // Act & Assert: API quản trị trả 401 và tuyệt đối không trả HTML redirect.
        using var response = await client.GetAsync(route);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task AnonymousOwnPasswordMutationReturns401WithoutRedirect()
    {
        // Act: thử mutation đổi mật khẩu khi chưa có principal xác thực.
        using var response = await client.PutAsJsonAsync(
            "/api/v1/users/me/password",
            new { currentPassword = "Current@123", newPassword = "Changed@123" });

        // Assert: authorization chạy trước command và không lộ trang login cho API client.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Theory]
    [InlineData("/api/v1/production-plans/bulk-preview")]
    [InlineData("/api/v1/production-plans/bulk")]
    [InlineData("/api/v1/production-plan-samples/bulk")]
    [InlineData("/api/v1/production-plan-samples/00000000-0000-0000-0000-000000000001/items/bulk-preview")]
    [InlineData("/api/v1/production-plan-samples/00000000-0000-0000-0000-000000000001/items/bulk")]
    public async Task AnonymousBulkPlanMutationsReturn401(string route)
    {
        // Act & Assert: các bulk command Production đều kiểm tra quyền phía server.
        using var response = await client.PostAsJsonAsync(route, new { });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Theory]
    [InlineData("/api/v1/alerts/00000000-0000-0000-0000-000000000001/acknowledgement")]
    [InlineData("/api/v1/alerts/00000000-0000-0000-0000-000000000001/resolution")]
    [InlineData("/api/v1/alerts/00000000-0000-0000-0000-000000000001/reevaluation")]
    public async Task AnonymousAlertMutationsReturn401(string route)
    {
        // Act & Assert: các transition cảnh báo không thể gọi ẩn danh dù client gửi payload hợp lệ hình thức.
        using var response = await client.PostAsJsonAsync(route, new { concurrencyToken = DateTimeOffset.UtcNow.ToString("O") });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task LoginRejectsExternalReturnUrl()
    {
        // Act: gửi ReturnUrl dạng protocol-relative có thể dẫn tới open redirect.
        using var response = await client.GetAsync("/Account/Login?returnUrl=%2F%2Fevil.example");
        var html = await response.Content.ReadAsStringAsync();

        // Assert: server thay URL ngoài bằng trang gốc nội bộ.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("name=\"returnUrl\" value=\"/\"", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("value=\"//evil.example\"", html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("en-US")]
    public async Task LanguageHeadersQueryAndCookieCannotSwitchAwayFromVietnamese(string culture)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/Account/Login?culture={culture}&ui-culture={culture}");
        request.Headers.Add("Accept-Language", culture);
        request.Headers.Add("Cookie", $".AspNetCore.Culture=c={culture}|uic={culture}");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("vi-VN", response.Content.Headers.ContentLanguage);
    }
}
