using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Oee.Web.Components.Layout;

namespace Oee.Web.ComponentTests;

public sealed class WorkspaceTabsTests : BunitContext, IAsyncLifetime
{
    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;
    async Task IAsyncLifetime.DisposeAsync() => await base.DisposeAsync();
    public WorkspaceTabsTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
    }

    [Fact]
    public void NavigationOpensUniqueTabsAndClosingActiveTabNavigatesToNeighbour()
    {
        var navigation = Services.GetRequiredService<NavigationManager>();
        var state = new WorkspaceTabState();
        var component = Render<WorkspaceTabs>(parameters => parameters.Add(item => item.State, state));
        navigation.NavigateTo("/master/products");
        navigation.NavigateTo("/production/plans");
        navigation.NavigateTo("/master/products");

        component.WaitForAssertion(() => Assert.Equal(3, component.FindAll(".workspace-tab").Count));
        Assert.Equal("/master/products", component.Find("a[aria-current='page']").GetAttribute("href"));
        component.Find("button[aria-label='Đóng tab Sản phẩm / SKU']").Click();

        Assert.EndsWith("/production/plans", navigation.Uri, StringComparison.Ordinal);
        Assert.Equal(2, state.Tabs.Count);
        Assert.Null(state.Close("/"));
        Assert.Contains(state.Tabs, item => item.Url == "/");
    }

    [Fact]
    public void ClosingInactiveTabKeepsCurrentPageAndDynamicTitleIsUpdated()
    {
        var state = new WorkspaceTabState();
        state.Open("/master/products");
        state.Open("/production/plan-templates/00000000-0000-0000-0000-000000000001");
        state.SetTitle("KHM-001 — Wakeup");

        Assert.Null(state.Close("/master/products"));
        Assert.Equal("KHM-001 — Wakeup", state.Tabs[^1].Title);
        Assert.Equal(state.Tabs[^1].Url, state.ActiveUrl);
    }

    [Fact]
    public void NewPageTitleDoesNotRenamePreviousTabWhenHeaderRendersBeforeLocationListener()
    {
        var state = new WorkspaceTabState();
        state.Open("/master/products");
        state.SetTitle("Kế hoạch sản xuất", "/production/plans");
        state.Open("/production/plans");
        Assert.Equal("Sản phẩm / SKU", state.Tabs.Single(item => item.Url == "/master/products").Title);
        Assert.Equal("Kế hoạch sản xuất", state.Tabs.Single(item => item.Url == "/production/plans").Title);
    }

    [Fact]
    public void RepeatedNavigationAndTitleUpdatesDoNotRequestDuplicateTabRenders()
    {
        var state = new WorkspaceTabState();
        var changes = 0;
        state.Changed += () => changes++;
        state.SetTitle("Kế hoạch sản xuất", "/production/plans");
        Assert.Equal(1, changes);
        for (var index = 0; index < 20; index++)
        {
            state.Open("/production/plans");
            state.SetTitle("Kế hoạch sản xuất", "/production/plans");
        }
        Assert.Equal(1, changes);
    }

    [Fact]
    public void TabListIsBoundedAndDoesNotAcceptExternalOrLoginUrls()
    {
        var state = new WorkspaceTabState();
        for (var index = 0; index < 100; index++) state.Open($"/production/plans?date={index}");
        Assert.Equal(WorkspaceTabState.MaximumTabs, state.Tabs.Count);
        Assert.Equal("/", state.Tabs[0].Url);
        var active = state.ActiveUrl;
        state.Open("https://example.com");
        state.Open("//example.com");
        state.Open("/account/login");
        Assert.Equal(active, state.ActiveUrl);
    }
}
