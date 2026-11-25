using Bunit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.PlcIntegration;
using Oee.Application.Security;
using Oee.Web.Components.Pages.Plc;
using Oee.Web.Realtime;
using Oee.Application.Common;
using Oee.Application.PlcIntegration.Configuration.Connections.Queries;
using Oee.Application.PlcIntegration.Monitoring;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Oee.Web.ComponentTests;

/// <summary>
/// bUnit Test cho các page PLC, dialog cấu hình OPC UA và fault mapping.
/// </summary>
public sealed class PlcPagesTests : RowMenuTestContext, IAsyncLifetime
{
    private readonly FakePlcConfiguration configuration = new();

    /// <summary>Đăng ký principal PlanningManager cùng fake PLC services; không cần SDK hoặc PLC thật.</summary>
    public PlcPagesTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddAuthorizationCore(options => options.AddPolicy(Capabilities.PlcManage,
            policy => policy.RequireRole("Admin", "PlanningManager")));
        var authorization = this.AddAuthorization(); authorization.SetAuthorized("planner");
        authorization.SetRoles("PlanningManager"); authorization.SetPolicies(Capabilities.PlcManage);
        Services.AddSingleton<IPlcConfigurationScenario>(configuration).AddScenarioRequests();
        Services.AddSingleton<RealtimeOeeState>();
    }

    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;
    async Task IAsyncLifetime.DisposeAsync() => await base.DisposeAsync();

    [Fact]
    public void SignedInReaderCanSeeMappingDataWithoutMutationButtons()
    {
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("reader");
        authorization.SetRoles();
        authorization.SetPolicies();

        var component = Render<PlcDeviceMappings>();

        component.WaitForAssertion(() =>
        {
            Assert.Contains("PLC-M-001", component.Markup, StringComparison.Ordinal);
            Assert.Empty(component.FindAll("button[aria-label='Sửa cấu hình OPC UA']"));
            Assert.Empty(component.FindAll("button[aria-label='Xóa mapping']"));
        });
    }

    [Fact]
    public void MappingAndFaultListsUseServerBackedRowsAndActions()
    {
        // Act & Assert: mapping page dùng dữ liệu service và hiển thị count Read/Write thực tế.
        var mappings = Render<PlcDeviceMappings>();
        mappings.WaitForAssertion(() =>
        {
            Assert.Contains("PLC-M-001", mappings.Markup, StringComparison.Ordinal);
            Assert.Contains("3/5 read", mappings.Markup, StringComparison.Ordinal);
            Assert.Contains("1/9 write", mappings.Markup, StringComparison.Ordinal);
        });

        // Act & Assert: fault page nhận row đã phân trang và cung cấp import XLSX.
        var faults = Render<FaultMappings>();
        faults.WaitForAssertion(() =>
        {
            Assert.Contains("16#001A", faults.Markup, StringComparison.Ordinal);
            Assert.Contains("Import XLSX", faults.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void MappingRowsLoadWithoutEditorCatalogAndLookupFailureIsShownOnEdit()
    {
        configuration.FailLookups = true;
        var component = Render<PlcDeviceMappings>();
        component.WaitForAssertion(() => Assert.Contains("PLC-M-001", component.Markup, StringComparison.Ordinal));
        Assert.Equal(0, configuration.LookupRequests);

        component.Find("button[aria-label='Sửa cấu hình OPC UA']").Click();
        component.WaitForAssertion(() => Assert.Contains("Không thể tải danh mục cấu hình OPC UA", component.Markup, StringComparison.Ordinal));
        Assert.Equal(1, configuration.LookupRequests);
        Assert.DoesNotContain("internal-lookup-detail", component.Markup, StringComparison.Ordinal);
        Assert.Contains("PLC-M-001", component.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpcConfigurationDialogContainsRequiredInputsOutputsAndNoSecretField()
    {
        // Arrange: render MudDialogProvider thật và truyền lookup/connection DTO đã làm sạch.
        var provider = Render<MudDialogProvider>();
        var parameters = new DialogParameters<OpcUaConfigurationDialog>
        {
            { x => x.Lookups, configuration.Lookups }, { x => x.Connections, configuration.ConnectionItems },
        };
        // Act: mở dialog qua IDialogService giống luồng người dùng thật.
        await Services.GetRequiredService<IDialogService>().ShowAsync<OpcUaConfigurationDialog>("OPC UA", parameters);
        // Assert: đủ tag/counter tùy chọn và không render secret reference trên form.
        provider.WaitForAssertion(() =>
        {
            var tags = provider.FindComponents<TagConfigurationRow>().Select(x => x.Instance.Tag).ToArray();
            Assert.Equal(14, tags.Length);
            Assert.Equal(2, tags.Count(x => x.Required));
            Assert.Contains(tags, x => x.Role == "Running" && x.Required);
            Assert.Contains(tags, x => x.Role == "TotalCounter" && x.Required);
            Assert.Contains(tags, x => x.Role == "Mtbf" && !x.Required);
            Assert.Contains(tags, x => x.Role == "Mttr" && !x.Required);
            Assert.Contains("Định dạng mã lỗi", provider.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("Đồng bộ mốc counter hiện tại khi lưu", provider.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("CredentialSecretReference", provider.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void MappingMetadataEditUsesSeparatePopupAndPreservesTagsAndCounterBaseline()
    {
        _ = Menus;
        var dialogs = Render<MudDialogProvider>();
        var page = Render<PlcDeviceMappings>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".row-action-menu")));
        SelectAction(page, "Sửa mapping");
        dialogs.WaitForAssertion(() => Assert.Single(dialogs.FindComponents<PlcMappingEditorDialog>()));
        Assert.Empty(dialogs.FindComponents<OpcUaConfigurationDialog>());
        Assert.Empty(dialogs.FindComponents<TagConfigurationRow>());
        var field = dialogs.FindComponents<MudTextField<string>>().Single(item => item.Instance.Label == "Mã thiết bị PLC/SCADA");
        field.Find("input").Change("PLC-UPDATED");
        dialogs.FindAll("button").Single(button => button.TextContent.Trim() == "Lưu").Click();
        dialogs.WaitForAssertion(() => Assert.NotNull(configuration.LastSaved));
        Assert.Equal("PLC-UPDATED", configuration.LastSaved!.ExternalDeviceId);
        Assert.Equal(4, configuration.LastSaved.Tags.Count);
        Assert.Equal(Convert.ToBase64String([1]), configuration.LastSaved.RowVersion);
    }

    [Fact]
    public void NewMappingStartsWithMetadataPopupAndCancelDoesNotWrite()
    {
        _ = Menus;
        var dialogs = Render<MudDialogProvider>();
        var page = Render<PlcDeviceMappings>();
        page.WaitForAssertion(() => Assert.Contains("PLC-M-001", page.Markup, StringComparison.Ordinal));
        page.FindAll("button").Single(button => button.TextContent.Trim() == "THÊM ÁNH XẠ").Click();
        dialogs.WaitForAssertion(() => Assert.Single(dialogs.FindComponents<PlcMappingEditorDialog>()));
        Assert.Empty(dialogs.FindComponents<OpcUaConfigurationDialog>());
        dialogs.FindAll("button").Single(button => button.TextContent.Trim() == "Hủy").Click();
        dialogs.WaitForAssertion(() => Assert.Empty(dialogs.FindComponents<PlcMappingEditorDialog>()));
        Assert.Null(configuration.LastSaved);
    }

    [Fact]
    public async Task NodeTreeLoadsFoldersLazilyCachesChildrenAndRequiresVariableConfirmation()
    {
        configuration.TreeBrowse = true;
        var provider = Render<MudDialogProvider>();
        var dialog = await Services.GetRequiredService<IDialogService>().ShowAsync<OpcUaNodeBrowserDialog>("Tags",
            new DialogParameters<OpcUaNodeBrowserDialog> { { x => x.ConnectionId, configuration.ConnectionId } });
        provider.WaitForAssertion(() => Assert.Single(provider.FindComponents<MudTreeViewItem<OpcUaBrowseNode>>()));
        Assert.Empty(provider.FindAll("table"));
        Assert.Single(configuration.BrowseRequests);
        var root = provider.FindComponents<MudTreeViewItem<OpcUaBrowseNode>>().Single(x => x.Instance.Value?.NodeId == "ns=8;s=Plant");
        root.Find(".mud-treeview-item-content").Click();
        Assert.True(provider.FindAll("button").Single(x => x.TextContent.Trim() == "Chọn tag này").HasAttribute("disabled"));
        root.Find("button").Click();
        provider.WaitForAssertion(() => Assert.Contains(configuration.BrowseRequests, x => x.ParentNodeId == "ns=8;s=Plant"));
        var branch = provider.FindComponents<MudTreeViewItem<OpcUaBrowseNode>>().Single(x => x.Instance.Value?.NodeId == "ns=8;s=Packing");
        branch.Find("button").Click();
        provider.WaitForAssertion(() => Assert.Contains("RunStopSig", provider.Markup, StringComparison.Ordinal));
        var requests = configuration.BrowseRequests.Count;
        root.Find("button").Click();
        root.Find("button").Click();
        Assert.Equal(requests, configuration.BrowseRequests.Count);
        provider.FindComponents<MudTreeViewItem<OpcUaBrowseNode>>().Single(x => x.Instance.Value?.DisplayName == "RunStopSig")
            .Find(".mud-treeview-item-content").Click();
        provider.WaitForAssertion(() => Assert.Contains("Đang chọn", provider.Markup, StringComparison.Ordinal));
        Assert.False(dialog.Result.IsCompleted);
        provider.FindAll("button").Single(x => x.TextContent.Trim() == "Chọn tag này").Click();
        var result = await dialog.Result;
        var chosen = Assert.IsType<OpcUaBrowseNode>(result!.Data);
        Assert.Equal("ns=8;s=Run", chosen.NodeId);
        Assert.Equal("Boolean", chosen.DataType);
        Assert.True(chosen.IsVariable);
    }

    [Fact]
    public async Task NodePickerReturnsNodeIdAndDataTypeWithoutSavingUntilTheConfigurationIsSaved()
    {
        configuration.TreeBrowse = true;
        _ = Menus;
        var provider = Render<MudDialogProvider>();
        await Services.GetRequiredService<IDialogService>().ShowAsync<OpcUaConfigurationDialog>("Configuration",
            new DialogParameters<OpcUaConfigurationDialog>
            {
                { x => x.MappingId, configuration.DeviceId }, { x => x.Lookups, configuration.Lookups },
                { x => x.Connections, configuration.ConnectionItems },
            });
        provider.WaitForAssertion(() => Assert.Equal(14, provider.FindComponents<TagConfigurationRow>().Count));
        var total = provider.FindComponents<TagConfigurationRow>().Single(x => x.Instance.Tag.Role == "TotalCounter");
        Assert.False(total.Find("input").HasAttribute("readonly"));
        Assert.Equal("ns=2;s=Total", total.Find("input").GetAttribute("value"));
        total.Find("button[aria-label='Browse tag TotalCounter']").Click();
        provider.WaitForAssertion(() => Assert.Single(provider.FindComponents<OpcUaNodeBrowserDialog>()));
        // Mở từ root ngay cả khi NodeId cũ là leaf, không browse con của leaf hiện tại.
        Assert.Null(configuration.BrowseRequests.Last().ParentNodeId);
        provider.FindComponent<OpcUaNodeBrowserDialog>().FindAll("button").Single(x => x.TextContent.Trim() == "Hủy").Click();
        Assert.Equal("ns=2;s=Total", total.Instance.Tag.NodeId);
        total.Find("button[aria-label='Browse tag TotalCounter']").Click();
        provider.WaitForAssertion(() => Assert.Single(provider.FindComponents<OpcUaNodeBrowserDialog>()));
        var browser = provider.FindComponent<OpcUaNodeBrowserDialog>();
        browser.FindComponents<MudTreeViewItem<OpcUaBrowseNode>>().Single(x => x.Instance.Value?.NodeId == "ns=8;s=Plant").Find("button").Click();
        browser.WaitForAssertion(() => Assert.Contains("Packing", browser.Markup, StringComparison.Ordinal));
        browser.FindComponents<MudTreeViewItem<OpcUaBrowseNode>>().Single(x => x.Instance.Value?.NodeId == "ns=8;s=Packing").Find("button").Click();
        browser.WaitForAssertion(() => Assert.Contains("TotalCount", browser.Markup, StringComparison.Ordinal));
        browser.FindComponents<MudTreeViewItem<OpcUaBrowseNode>>().Single(x => x.Instance.Value?.DisplayName == "TotalCount").Find(".mud-treeview-item-content").Click();
        browser.FindAll("button").Single(x => x.TextContent.Trim() == "Chọn tag này").Click();
        provider.WaitForAssertion(() => Assert.Equal("ns=8;s=Total", total.Instance.Tag.NodeId));
        provider.WaitForAssertion(() => Assert.Equal("ns=8;s=Total", total.Find("input").GetAttribute("value")));
        Assert.DoesNotContain("Tag.DisplayName", provider.Markup, StringComparison.Ordinal);
        Assert.Equal("Unsigned32", total.Instance.Tag.DataType);
        Assert.Equal("TotalCount", total.Instance.Tag.DisplayName);
        Assert.Null(configuration.LastSaved);
        provider.FindAll("button").Single(x => x.TextContent.Trim() == "LƯU CẤU HÌNH").Click();
        provider.WaitForAssertion(() => Assert.NotNull(configuration.LastSaved));
        Assert.Contains(configuration.LastSaved!.Tags, x => x.Role == "TotalCounter" && x.NodeId == "ns=8;s=Total" && x.DataType == "Unsigned32");
    }

    [Fact]
    public async Task UnsavedNodeCanBeTypedAndReadWithoutSavingTheMapping()
    {
        var executor = new TagReadExecutor(configuration);
        Services.RemoveAll<IUseCaseExecutor>();
        Services.AddSingleton<IUseCaseExecutor>(executor);
        var dialogs = Render<MudDialogProvider>();
        await Services.GetRequiredService<IDialogService>().ShowAsync<OpcUaConfigurationDialog>("OPC",
            new DialogParameters<OpcUaConfigurationDialog>
            {
                { component => component.MappingId, configuration.DeviceId },
                { component => component.Lookups, configuration.Lookups },
                { component => component.Connections, configuration.ConnectionItems }
            });
        dialogs.WaitForAssertion(() => Assert.Equal(14, dialogs.FindComponents<TagConfigurationRow>().Count));
        var row = dialogs.FindComponents<TagConfigurationRow>().Single(component => component.Instance.Tag.Role == "TotalCounter");
        row.Find("input").Change("ns=8;s=NewCounter");
        row.Find("button[aria-label='Đọc thử tag TotalCounter']").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("2861", row.Markup, StringComparison.Ordinal));
        Assert.Equal("ns=8;s=NewCounter", executor.LastQuery!.NodeId);
        Assert.Equal(configuration.ConnectionId, executor.LastQuery.ConnectionId);
        Assert.Null(configuration.LastSaved);
    }

    private sealed class TagReadExecutor(IPlcConfigurationScenario configuration) : IUseCaseExecutor
    {
        private readonly ScenarioUseCaseExecutor fallback = new(type => type == typeof(IPlcConfigurationScenario) ? configuration : null);
        public TestOpcUaTagQuery? LastQuery { get; private set; }
        public Task<TResult> ExecuteAsync<TResult>(IRequest<TResult> request, CancellationToken cancellationToken = default)
        {
            if (request is not TestOpcUaTagQuery query) return fallback.ExecuteAsync(request, cancellationToken);
            LastQuery = query;
            object value = new OpcTagDirectRead(query.NodeId, "2861", "Decimal", true, "Good", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
            return Task.FromResult((TResult)value);
        }
        public Task ExecuteAsync(ICommand command, CancellationToken cancellationToken = default) => fallback.ExecuteAsync(command, cancellationToken);
    }

    [Fact]
    public async Task NodeTreeRootFailureIsSanitizedAndCanRetry()
    {
        configuration.TreeBrowse = true;
        configuration.FailBrowse = true;
        var provider = Render<MudDialogProvider>();
        var dialog = await Services.GetRequiredService<IDialogService>().ShowAsync<OpcUaNodeBrowserDialog>("Tags",
            new DialogParameters<OpcUaNodeBrowserDialog> { { x => x.ConnectionId, configuration.ConnectionId } });
        provider.WaitForAssertion(() => Assert.Contains("Không thể tải cây tag", provider.Markup, StringComparison.Ordinal));
        Assert.DoesNotContain("password=hidden", provider.Markup, StringComparison.Ordinal);
        configuration.FailBrowse = false;
        provider.FindAll("button").Single(x => x.TextContent.Trim() == "Thử lại").Click();
        provider.WaitForAssertion(() => Assert.Contains("CSDHG", provider.Markup, StringComparison.Ordinal));
        provider.FindAll("button").Single(x => x.TextContent.Trim() == "Hủy").Click();
        Assert.True((await dialog.Result)!.Canceled);
    }

    [Fact]
    public async Task NodeTreeFailedBranchRetriesThroughNativeTreeWithoutLosingAncestors()
    {
        configuration.TreeBrowse = true;
        configuration.FailNextBranch = true;
        var provider = Render<MudDialogProvider>();
        await Services.GetRequiredService<IDialogService>().ShowAsync<OpcUaNodeBrowserDialog>("Tags",
            new DialogParameters<OpcUaNodeBrowserDialog> { { x => x.ConnectionId, configuration.ConnectionId } });
        provider.WaitForAssertion(() => Assert.Single(provider.FindComponents<MudTreeViewItem<OpcUaBrowseNode>>()));
        provider.FindComponents<MudTreeViewItem<OpcUaBrowseNode>>().Single().Find("button").Click();
        provider.WaitForAssertion(() => Assert.Contains("Thử lại nhánh", provider.Markup, StringComparison.Ordinal));
        provider.Find("button[aria-label='Thử lại nhánh CSDHG']").Click();
        provider.WaitForAssertion(() => Assert.Contains("Packing", provider.Markup, StringComparison.Ordinal));
        Assert.Contains("CSDHG", provider.Markup, StringComparison.Ordinal);
        Assert.Equal(2, configuration.BrowseRequests.Count(x => x.ParentNodeId == "ns=8;s=Plant"));
    }

    [Theory]
    [InlineData(typeof(OpcUaConnections))]
    [InlineData(typeof(PlcDeviceMappings))]
    [InlineData(typeof(FaultMappings))]
    public void PlcRoutesRequireSignInWithoutManagementCapability(Type pageType)
    {
        // Arrange & Act: đọc AuthorizeAttribute của từng page PLC bằng reflection.
        var authorize = pageType.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>().Single();
        // Trang xem vẫn yêu cầu đăng nhập; quyền PlcManage được giữ ở từng thao tác ghi.
        Assert.Null(authorize.Policy);
        Assert.Null(authorize.Roles);
        Assert.Empty(pageType.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true));
    }

    [Fact]
    public void ConnectionCreateOpensModalValidatesAndCancelDoesNotWrite()
    {
        _ = Menus;
        var dialogs = Render<MudDialogProvider>();
        var page = Render<OpcUaConnections>();
        page.WaitForAssertion(() => Assert.Contains("OPC-01", page.Markup, StringComparison.Ordinal));
        page.FindAll("button").Single(x => x.TextContent.Trim() == "Thêm kết nối").Click();
        dialogs.WaitForAssertion(() => Assert.Single(dialogs.FindComponents<OpcUaConnectionEditorDialog>()));
        Assert.Empty(page.FindComponents<OpcUaConnectionEditorDialog>());
        dialogs.FindAll("button").Single(x => x.TextContent.Trim() == "Lưu").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("mud-input-error", dialogs.Markup, StringComparison.Ordinal));
        Assert.Equal(0, configuration.CreateConnectionCalls);
        dialogs.FindAll("button").Single(x => x.TextContent.Trim() == "Hủy").Click();
        dialogs.WaitForAssertion(() => Assert.Empty(dialogs.FindComponents<OpcUaConnectionEditorDialog>()));
        Assert.Equal(0, configuration.CreateConnectionCalls);
    }

    [Fact]
    public void NewConnectionSavesFromModalAndReloadsListOnlyAfterSuccess()
    {
        _ = Menus;
        var dialogs = Render<MudDialogProvider>();
        var page = Render<OpcUaConnections>();
        page.FindAll("button").Single(x => x.TextContent.Trim() == "Thêm kết nối").Click();
        dialogs.WaitForAssertion(() => Assert.Single(dialogs.FindComponents<OpcUaConnectionEditorDialog>()));
        void Set(string label, string value) => dialogs.FindComponents<MudTextField<string>>().Single(x => x.Instance.Label == label).Find("input").Change(value);
        Set("Mã kết nối", "OPC-NEW");
        Set("Tên kết nối", "New OPC");
        Set("Endpoint opc.tcp://", "opc.tcp://localhost:4840");
        var before = configuration.ConnectionListCalls;
        dialogs.FindAll("button").Single(x => x.TextContent.Trim() == "Lưu").Click();
        dialogs.WaitForAssertion(() => Assert.Equal(1, configuration.CreateConnectionCalls));
        dialogs.WaitForAssertion(() => Assert.Empty(dialogs.FindComponents<OpcUaConnectionEditorDialog>()));
        page.WaitForAssertion(() => Assert.True(configuration.ConnectionListCalls > before));
        Assert.Equal("OPC-NEW", configuration.LastConnectionRequest!.Code);
    }

    [Fact]
    public void ConnectionEditKeepsSecretBlankAndPreservesTokenOnFailureAndRetry()
    {
        _ = Menus;
        var dialogs = Render<MudDialogProvider>();
        var page = Render<OpcUaConnections>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".row-action-menu")));
        SelectAction(page, "Sửa kết nối");
        dialogs.WaitForAssertion(() => Assert.Single(dialogs.FindAll("input[type='password']")));
        Assert.True(string.IsNullOrEmpty(dialogs.Find("input[type='password']").GetAttribute("value")));
        var name = dialogs.FindComponents<MudTextField<string>>().Single(x => x.Instance.Label == "Tên kết nối");
        name.Find("input").Change("Main OPC updated");
        configuration.FailConnectionSave = true;
        dialogs.FindAll("button").Single(x => x.TextContent.Trim() == "Lưu").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Không thể lưu kết nối", dialogs.Markup, StringComparison.Ordinal));
        Assert.Single(dialogs.FindComponents<OpcUaConnectionEditorDialog>());
        Assert.DoesNotContain("internal-backend-detail", dialogs.Markup, StringComparison.Ordinal);
        Assert.Null(configuration.LastConnectionRequest!.CredentialSecretReference);
        Assert.Equal(Convert.ToBase64String([1]), configuration.LastConnectionRequest.RowVersion);
        Assert.Equal(configuration.ConnectionId, configuration.LastConnectionId);
        configuration.FailConnectionSave = false;
        var before = configuration.ConnectionListCalls;
        dialogs.FindAll("button").Single(x => x.TextContent.Trim() == "Lưu").Click();
        dialogs.WaitForAssertion(() => Assert.Empty(dialogs.FindComponents<OpcUaConnectionEditorDialog>()));
        page.WaitForAssertion(() => Assert.True(configuration.ConnectionListCalls > before));
        Assert.Equal("Main OPC updated", configuration.LastConnectionRequest.Name);
    }

    [Fact]
    public void FaultMappingEditUsesPopupAndRetainsDisplayedScopeAndToken()
    {
        _ = Menus;
        var dialogs = Render<MudDialogProvider>();
        var page = Render<FaultMappings>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".row-action-menu")));
        SelectAction(page, "Sửa fault mapping");
        dialogs.WaitForAssertion(() => Assert.Single(dialogs.FindComponents<FaultMappingEditorDialog>()));
        Assert.Empty(page.FindComponents<FaultMappingEditorDialog>());
        var item = dialogs.FindComponent<FaultMappingEditorDialog>().Instance.Item!;
        dialogs.FindComponents<MudTextField<string>>().Single(x => x.Instance.Label == "Fault code").Find("input").Change("27");
        dialogs.FindAll("button").Single(x => x.TextContent.Trim() == "Lưu").Click();
        dialogs.WaitForAssertion(() => Assert.NotNull(configuration.LastFaultRequest));
        Assert.Equal(item.WorkUnitId, configuration.LastFaultRequest!.WorkUnitId);
        Assert.Equal(item.RowVersion, configuration.LastFaultRequest.RowVersion);
        Assert.Equal("27", configuration.LastFaultRequest.FaultCode);
        dialogs.WaitForAssertion(() => Assert.Empty(dialogs.FindComponents<FaultMappingEditorDialog>()));
    }

    [Fact]
    public async Task BlockedCounterExplainsAutomaticRecoveryAndCancelDoesNotWrite()
    {
        configuration.CounterBlocked = true;
        var provider = Render<MudDialogProvider>();
        await Services.GetRequiredService<IDialogService>().ShowAsync<OpcUaConfigurationDialog>("Configuration",
            new DialogParameters<OpcUaConfigurationDialog>
            {
                { x => x.MappingId, configuration.DeviceId }, { x => x.Lookups, configuration.Lookups },
                { x => x.Connections, configuration.ConnectionItems }
            });
        provider.WaitForAssertion(() => Assert.Contains("Counter nguồn đã giảm", provider.Markup, StringComparison.Ordinal));
        Assert.Contains("Nhận OPC gần nhất", provider.Markup, StringComparison.Ordinal);
        Assert.Contains("Counter được chấp nhận gần nhất", provider.Markup, StringComparison.Ordinal);
        Assert.Contains("Hệ thống tự đọc lại counter", provider.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Đồng bộ mốc hiện tại", provider.Markup, StringComparison.Ordinal);
        provider.FindAll("button").Single(x => x.TextContent.Trim() == "Hủy").Click();
        Assert.Null(configuration.LastSaved);
        provider.WaitForAssertion(() => Assert.Empty(provider.FindComponents<OpcUaConfigurationDialog>()));
    }

    [Fact]
    public async Task SavingBlockedMappingPreservesTagsWithoutBaselineAction()
    {
        configuration.CounterBlocked = true;
        var provider = Render<MudDialogProvider>();
        await Services.GetRequiredService<IDialogService>().ShowAsync<OpcUaConfigurationDialog>("Configuration",
            new DialogParameters<OpcUaConfigurationDialog>
            {
                { x => x.MappingId, configuration.DeviceId }, { x => x.Lookups, configuration.Lookups },
                { x => x.Connections, configuration.ConnectionItems }
            });
        provider.WaitForAssertion(() => Assert.Contains("Counter nguồn đã giảm", provider.Markup, StringComparison.Ordinal));
        provider.FindAll("button").Single(x => x.TextContent.Trim() == "LƯU CẤU HÌNH").Click();
        provider.WaitForAssertion(() => Assert.NotNull(configuration.LastSaved));
        Assert.Equal(configuration.ConnectionId, configuration.LastSaved!.ConnectionId);
        Assert.Equal(4, configuration.LastSaved.Tags.Count);
        Assert.Equal(Convert.ToBase64String([1]), configuration.LastSaved.RowVersion);
    }

    private sealed class FakePlcConfiguration : IPlcConfigurationScenario
    {
        public bool FailLookups { get; set; }
        public bool CounterBlocked { get; set; }
        public bool FailConnectionSave { get; set; }
        public int CreateConnectionCalls { get; private set; }
        public int ConnectionListCalls { get; private set; }
        public Guid? LastConnectionId { get; private set; }
        public OpcUaConnectionRequest? LastConnectionRequest { get; private set; }
        public PlcFaultMappingRequest? LastFaultRequest { get; private set; }
        public int LookupRequests { get; private set; }
        public Guid DeviceId { get; } = Guid.NewGuid();
        public Guid ConnectionId { get; } = Guid.NewGuid();
        public bool TreeBrowse { get; set; }
        public bool FailBrowse { get; set; }
        public bool FailNextBranch { get; set; }
        public List<OpcUaBrowseRequest> BrowseRequests { get; } = [];
        public PlcDeviceConfigurationRequest? LastSaved { get; private set; }
        private Guid WorkUnitId { get; } = Guid.NewGuid();
        private Guid ProductId { get; } = Guid.NewGuid();
        private Guid ReasonId { get; } = Guid.NewGuid();
        public PlcConfigurationLookups Lookups => new(
            [new(ConnectionId, "OPC-01", "Main OPC")], [new(WorkUnitId, "M-001", "Filler")],
            [new(ProductId, "SKU-01", "Bottle")], [new(ReasonId, "DR-01", "Failure")]);
        public IReadOnlyCollection<OpcUaConnectionItem> ConnectionItems => [Connection];
        private OpcUaConnectionItem Connection => new(ConnectionId, "OPC-01", "Main OPC", "opc.tcp://localhost:4840",
            "SignAndEncrypt", "Basic256Sha256", "UserName", "operator", true, "cert-ref", true,
            "Connected", DateTimeOffset.UtcNow, Convert.ToBase64String([1]));
        private PlcDeviceMappingItem Mapping => new(DeviceId, ConnectionId, "OPC-01", WorkUnitId, "M-001", ProductId,
            "SKU-01", "PLC-M-001", "Cumulative", "Auto", true, 2, null, 3, 1, DateTimeOffset.UtcNow,
            "Disconnected", true, Convert.ToBase64String([1]), DateTimeOffset.UtcNow,
            CounterBlocked, CounterBlocked ? 838197m : null, CounterBlocked ? 1324m : null);
        private static IReadOnlyCollection<PlcTagConfiguration> Tags =>
            [new(null, "Running", "Read", "ns=2;s=Run", "BooleanValue", true),
                new(null, "TotalCounter", "Read", "ns=2;s=Total", "DoublePrecision", true),
                new(null, "FaultCode", "Read", "ns=2;s=Fault", "Text", false),
                new(null, "Oee", "Write", "ns=2;s=Oee", "DoublePrecision", false)];

        public Task<PlcConfigurationLookups> LookupsAsync(CancellationToken cancellationToken)
        {
            LookupRequests++;
            if (FailLookups) throw new InvalidOperationException("internal-lookup-detail");
            return Task.FromResult(Lookups);
        }
        public Task<PagedResult<OpcUaConnectionItem>> ConnectionsAsync(PlcListQuery query, CancellationToken cancellationToken) { ConnectionListCalls++; return Task.FromResult(new PagedResult<OpcUaConnectionItem>([Connection], 1)); }
        public Task<Guid> CreateConnectionAsync(OpcUaConnectionRequest request, CancellationToken cancellationToken) { CreateConnectionCalls++; LastConnectionRequest = request; return Task.FromResult(ConnectionId); }
        public Task UpdateConnectionAsync(Guid id, OpcUaConnectionRequest request, CancellationToken cancellationToken) { LastConnectionId = id; LastConnectionRequest = request; if (FailConnectionSave) throw new InvalidOperationException("internal-backend-detail"); return Task.CompletedTask; }
        public Task DeleteConnectionAsync(Guid id, string rowVersion, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<OpcUaConnectionTestResult> TestConnectionAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(new OpcUaConnectionTestResult(true, "Connected", "OK", DateTimeOffset.UtcNow));
        public Task<PagedResult<PlcDeviceMappingItem>> DeviceMappingsAsync(PlcListQuery query, CancellationToken cancellationToken) => Task.FromResult(new PagedResult<PlcDeviceMappingItem>([Mapping], 1));
        public Task<PlcDeviceConfiguration> DeviceConfigurationAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(new PlcDeviceConfiguration(Mapping, Tags));
        public Task<Guid> CreateDeviceConfigurationAsync(PlcDeviceConfigurationRequest request, CancellationToken cancellationToken) => Task.FromResult(DeviceId);
        public Task UpdateDeviceConfigurationAsync(Guid id, PlcDeviceConfigurationRequest request, CancellationToken cancellationToken) { LastSaved = request; return Task.CompletedTask; }
        public Task DeleteDeviceConfigurationAsync(Guid id, string rowVersion, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyCollection<OpcUaBrowseNode>> BrowseAsync(OpcUaBrowseRequest request, CancellationToken cancellationToken)
        {
            BrowseRequests.Add(request);
            if (FailBrowse) throw new InvalidOperationException("password=hidden");
            if (FailNextBranch && request.ParentNodeId is not null) { FailNextBranch = false; throw new InvalidOperationException("password=hidden"); }
            if (!TreeBrowse) return Task.FromResult<IReadOnlyCollection<OpcUaBrowseNode>>([new("ns=2;s=Run", "Run", "Boolean", false)]);
            return Task.FromResult<IReadOnlyCollection<OpcUaBrowseNode>>(request.ParentNodeId switch
            {
                null => [new("ns=8;s=Plant", "CSDHG", null, true, false)],
                "ns=8;s=Plant" => [new("ns=8;s=Packing", "Packing", null, true, false)],
                "ns=8;s=Packing" => [new("ns=8;s=Run", "RunStopSig", "Boolean", false), new("ns=8;s=Total", "TotalCount", "UInt32", false)],
                _ => [],
            });
        }
        public Task<PagedResult<PlcFaultMappingItem>> FaultMappingsAsync(PlcListQuery query, CancellationToken cancellationToken) => Task.FromResult(new PagedResult<PlcFaultMappingItem>([new(Guid.NewGuid(), WorkUnitId, "M-001", "16#001A", ReasonId, "DR-01 — Failure", "Motor trip", true, Convert.ToBase64String([1]))], 1));
        public Task<Guid> CreateFaultMappingAsync(PlcFaultMappingRequest request, CancellationToken cancellationToken) => Task.FromResult(Guid.NewGuid());
        public Task UpdateFaultMappingAsync(Guid id, PlcFaultMappingRequest request, CancellationToken cancellationToken) { LastFaultRequest = request; return Task.CompletedTask; }
        public Task DeleteFaultMappingAsync(Guid id, string rowVersion, CancellationToken cancellationToken) => Task.CompletedTask;
    }

}
