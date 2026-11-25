using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Extensions;
using MudBlazor.Services;
using Oee.Web.Components.Shared;

namespace Oee.Web.ComponentTests;

public sealed class TableViewToolsTests : BunitContext, IAsyncLifetime
{
    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;
    async Task IAsyncLifetime.DisposeAsync() => await base.DisposeAsync();
    public TableViewToolsTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
    }

    [Fact]
    public async Task ColumnSettingsShowsActualColumnsCanHideAndRestoreAllWithoutExcel()
    {
        var module = JSInterop.SetupModule("./table-tools.js");
        TableViewColumn[] columns = [new("Mã:0", "Mã", true), new("Tên:0", "Tên", true)];
        var connect = module.Setup<TableViewColumn[]?>("connect", _ => true);
        connect.SetResult(columns);
        var popovers = Render<MudPopoverProvider>();
        var component = Render<TableViewTools>(parameters => parameters.Add(item => item.TableId, "test-table"));
        component.WaitForAssertion(() => Assert.False(component.Find("button").HasAttribute("disabled")));
        Assert.DoesNotContain("Excel", component.Markup, StringComparison.OrdinalIgnoreCase);
        component.Find("button[aria-label='Hiển thị cột']").Click();
        popovers.WaitForAssertion(() => Assert.Equal(2, popovers.FindComponents<MudCheckBox<bool>>().Count));

        TableViewColumn[] hidden = [columns[0], columns[1] with { Visible = false }];
        module.Setup<TableViewColumn[]>("setVisible", "test-table", "Tên:0", false).SetResult(hidden);
        connect.SetResult(hidden);
        var checkbox = popovers.FindComponents<MudCheckBox<bool>>().Single(item => item.Instance.Label == "Tên");
        await checkbox.InvokeAsync(() => checkbox.Instance.ValueChanged.InvokeAsync(false));

        popovers.WaitForAssertion(() => Assert.True(popovers.FindComponents<MudCheckBox<bool>>()
            .Single(item => item.Instance.Label == "Mã").Instance.Disabled));
        module.Setup<TableViewColumn[]>("showAll", "test-table").SetResult(columns);
        connect.SetResult(columns);
        popovers.FindAll("button").Single(item => item.TextContent.Trim() == "Hiện tất cả").Click();
        popovers.WaitForAssertion(() => Assert.All(popovers.FindComponents<MudCheckBox<bool>>(), item => Assert.True(item.Instance.GetState(check => check.Value))));
    }

    [Fact]
    public void TwentyParentRendersDoNotReconnectOrRescanTheSameTable()
    {
        var module = JSInterop.SetupModule("./table-tools.js");
        module.Setup<TableViewColumn[]?>("connect", _ => true)
            .SetResult([new("Máy:0", "Máy", true), new("SKU:0", "SKU", true)]);
        var component = Render<TableViewTools>(parameters => parameters
            .Add(item => item.TableId, "stable-table").Add(item => item.Title, "Bảng đang xem"));
        component.WaitForAssertion(() => Assert.Single(module.Invocations["connect"]));
        for (var index = 0; index < 20; index++)
            component.Render(parameters => parameters.Add(item => item.TableId, "stable-table")
                .Add(item => item.Title, "Bảng đang xem"));
        Assert.Single(module.Invocations["connect"]);
    }

    [Fact]
    public async Task AsyncTableLoadCanRefreshColumnListAfterInitialEmptyRender()
    {
        var module = JSInterop.SetupModule("./table-tools.js");
        var connect = module.Setup<TableViewColumn[]?>("connect", _ => true);
        connect.SetResult([]);
        var popovers = Render<MudPopoverProvider>();
        var component = Render<TableViewTools>(parameters => parameters.Add(item => item.TableId, "async-table"));
        TableViewColumn[] columns = [new("Thiết bị:0", "Thiết bị", true)];
        connect.SetResult(columns);
        await component.InvokeAsync(() => component.Instance.UpdateColumns(columns));
        component.Find("button").Click();
        popovers.WaitForAssertion(() => Assert.Equal("Thiết bị", popovers.FindComponent<MudCheckBox<bool>>().Instance.Label));
    }
}
