using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Oee.Application.Security;
using Oee.Web.Components.Layout;

namespace Oee.Web.ComponentTests;

/// <summary>
/// bUnit Test cho menu MudBlazor: active route, nhóm cha mở tự động và hành vi accordion.
/// </summary>
public sealed class AppNavigationTests : BunitContext
{
    /// <summary>Đăng ký MudBlazor và principal Admin để render toàn bộ nhóm menu được phân quyền.</summary>
    public AppNavigationTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddAuthorizationCore(options =>
        {
            foreach (var capability in Capabilities.All)
            {
                options.AddPolicy(capability, policy => policy.RequireAssertion(_ => true));
            }
        });
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("admin");
        authorization.SetRoles(RoleCapabilityMatrix.Admin);
        authorization.SetPolicies(Capabilities.All.ToArray());
    }

    [Fact]
    public void CurrentChildRouteExpandsAndHighlightsItsParentGroup()
    {
        // Arrange: điều hướng trực tiếp tới route con Kế hoạch sản xuất trước khi render menu.
        Services.GetRequiredService<NavigationManager>().NavigateTo("/production/plans");

        // Act: render AppNavigation bằng bUnit.
        var component = Render<AppNavigation>();

        // Assert: link con sáng đèn, đúng một nhóm cha active và expanded.
        component.WaitForAssertion(() =>
        {
            var currentLink = component.Find("a[href='/production/plans']");
            Assert.Contains("active", currentLink.ClassList);
            Assert.Contains("Kế hoạch sản xuất", currentLink.TextContent, StringComparison.Ordinal);
            Assert.Single(component.FindAll(".oee-nav-group--active"));
            Assert.Single(component.FindAll("button[aria-expanded='true']"));
            Assert.Empty(component.FindAll("a[href='/production/outputs/new'], a[href='/production/output-list']"));
        });
    }

    [Fact]
    public void SignedInReaderSeesDataNavigationWithoutWriteLinks()
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("reader");
        authorization.SetRoles();
        authorization.SetPolicies();
        Services.GetRequiredService<NavigationManager>().NavigateTo("/plc/mappings");

        var component = Render<AppNavigation>();

        component.WaitForAssertion(() =>
        {
            Assert.NotEmpty(component.FindAll("a[href='/plc/mappings']"));
            Assert.NotEmpty(component.FindAll("a[href='/users']"));
            Assert.Empty(component.FindAll("a[href='/production/outputs/new']"));
            Assert.Contains("Tích hợp PLC", component.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void NavigationChangeMovesActiveLightAndOpensTheMatchingGroup()
    {
        // Arrange: menu ban đầu ở Master data.
        var navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/master/products");
        var component = Render<AppNavigation>();

        // Act: mô phỏng người dùng chuyển route sang KPI ngày.
        navigation.NavigateTo("/reports/daily-oee");

        // Assert: đèn active được chuyển và chỉ nhóm Báo cáo mở.
        component.WaitForAssertion(() =>
        {
            Assert.Contains("active", component.Find("a[href='/reports/daily-oee']").ClassList);
            Assert.DoesNotContain("active", component.Find("a[href='/master/products']").ClassList);
            Assert.Single(component.FindAll(".oee-nav-group--active"));
            Assert.Single(component.FindAll("button[aria-expanded='true']"));
            Assert.Empty(component.FindAll("a[href='/reports'], a[href='/reports/oee'], a[href='/reports/oee-analytics'], a[href='/reports/snapshots'], a[href='/reports/plan-vs-actual'], a[href='/reports/downtime-pareto'], a[href='/production/status'], a[href='/production-status']"));
        });
    }

    [Fact]
    public void ParentGroupsAreNativeMudBlazorButtonsAndWorkAsAnAccordion()
    {
        // Arrange: render menu khi nhóm Sản xuất đang mở theo route hiện tại.
        Services.GetRequiredService<NavigationManager>().NavigateTo("/production/plans");
        var component = Render<AppNavigation>();

        // Act: bấm nhóm Dữ liệu nền bằng button thực mà MudBlazor render.
        var groups = component.FindAll("button.oee-nav-group");
        Assert.True(groups.Count >= 2);
        var masterGroup = groups[1];
        masterGroup.Click();

        // Assert: nhóm mới mở, nhóm cũ đóng và aria-expanded phản ánh đúng trạng thái hỗ trợ accessibility.
        component.WaitForAssertion(() =>
        {
            var updatedGroups = component.FindAll("button.oee-nav-group");
            Assert.Equal("true", updatedGroups[1].GetAttribute("aria-expanded"));
            Assert.Equal("false", updatedGroups[0].GetAttribute("aria-expanded"));
            Assert.Single(component.FindAll("button[aria-expanded='true']"));
        });
    }

    [Theory]
    [InlineData("/equipment/hierarchy")]
    public void EquipmentHierarchyHasOneTreeManagementEntry(string route)
    {
        Services.GetRequiredService<NavigationManager>().NavigateTo(route);
        var component = Render<AppNavigation>();
        string[] expected = ["/equipment/hierarchy"];

        component.WaitForAssertion(() =>
        {
            var selected = component.Find($"a[href='{route}']");
            Assert.Contains("active", selected.ClassList);
            var group = selected.Closest(".mud-nav-group");
            Assert.NotNull(group);
            Assert.Equal(expected, group.QuerySelectorAll("a[href]").Select(link => link.GetAttribute("href")));
            Assert.Single(component.FindAll(".oee-nav-group--active"));
            Assert.Single(component.FindAll("button[aria-expanded='true']"));
        });
    }
}
