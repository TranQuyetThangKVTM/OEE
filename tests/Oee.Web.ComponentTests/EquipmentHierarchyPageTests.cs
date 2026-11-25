using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Extensions;
using MudBlazor.Services;
using Oee.Application.Contracts.Modules;
using Oee.Application.Modules;
using Oee.Application.Security;
using Oee.Web.Components.Pages.Master;

namespace Oee.Web.ComponentTests;

public sealed class EquipmentHierarchyPageTests : RowMenuTestContext, IAsyncLifetime
{
    private static readonly int[] ExpectedDepths = [0, 1, 2, 3, 4];
    private readonly BunitAuthorizationContext _authorization;
    private readonly TreeQuery _query = new();
    private readonly MasterDataPageTests.NoOpMasterCommands _commands = new();

    public EquipmentHierarchyPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddLogging();
        Services.AddAuthorizationCore(options =>
        {
            options.AddPolicy(Capabilities.MasterDataWrite, policy => policy.RequireRole("Admin", "PlanningManager"));
        });
        _authorization = this.AddAuthorization();
        _authorization.SetAuthorized("planner");
        _authorization.SetRoles("PlanningManager");
        _authorization.SetPolicies(Capabilities.MasterDataRead, Capabilities.MasterDataWrite);
        Services.AddScoped<IEquipmentHierarchyQueryService>(_ => _query);
        Services.AddScoped<IMasterDataQueryService>(_ => new MasterDataPageTests.FakeMasterQuery());
        Services.AddScoped<IMasterDataScenario>(_ => _commands).AddScenarioRequests();
    }

    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;
    async Task IAsyncLifetime.DisposeAsync() => await base.DisposeAsync();

    [Fact]
    public void FirstBranchDisplaysFiveIndentedLevelsAndRealRuntimeStates()
    {
        var page = Render<EquipmentHierarchy>();
        page.WaitForAssertion(() => Assert.Equal(5, page.FindAll("[data-node]").Count));
        Assert.NotEmpty(page.FindComponents<MudTreeView<EquipmentHierarchyNode>>());
        Assert.Empty(page.FindAll("table"));
        Assert.Equal(5, page.FindAll(".hierarchy-node-description").Count);
        Assert.All(page.FindAll(".hierarchy-node-description"), node => Assert.Equal(node.TextContent, node.GetAttribute("title")));
        Assert.Equal(ExpectedDepths, page.FindAll("[data-node]").Select(NativeDepth));
        Assert.Contains("Running", page.Markup, StringComparison.Ordinal);
        Assert.Contains("Connected", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Work Unit Group", page.Markup, StringComparison.Ordinal);
        OpenActions(page, menuLabel: "Thao tác M-001");
        Assert.Contains("mud-disabled", MenuAction("Thêm con M-001").ClassName, StringComparison.Ordinal);
        OpenActions(page, menuLabel: "Thao tác ENT-01");
        Assert.Contains("mud-disabled", MenuAction("Xóa ENT-01").ClassName, StringComparison.Ordinal);
        Assert.Contains("LINE-01", page.Find(".mud-treeview-item-selected").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void CollapseAndReopenKeepChildrenCachedAndSelectTheParent()
    {
        var page = Render<EquipmentHierarchy>();
        page.WaitForAssertion(() => Assert.Equal(5, page.FindAll("[data-node]").Count));
        var requests = _query.Requests.Count;
        var root = page.FindComponents<MudTreeViewItem<EquipmentHierarchyNode>>().Single(x => x.Instance.Value?.Code == "ENT-01");
        root.Find("button").Click();
        page.WaitForAssertion(() => Assert.False(root.Instance.GetState(x => x.Expanded)));
        Assert.Equal("ENT-01", page.Find(".hierarchy-path-part").TextContent);
        root.Find("button").Click();
        page.WaitForAssertion(() => Assert.True(root.Instance.GetState(x => x.Expanded)));
        Assert.Equal(requests, _query.Requests.Count);
    }

    [Theory]
    [InlineData(EquipmentHierarchyKind.Enterprise, "Enterprise")]
    [InlineData(EquipmentHierarchyKind.Site, "Site")]
    [InlineData(EquipmentHierarchyKind.Area, "Area")]
    [InlineData(EquipmentHierarchyKind.WorkCenter, "Work Center")]
    [InlineData(EquipmentHierarchyKind.WorkUnit, "Work Unit")]
    public void FiveSearchFieldsSendTheCorrectLevelToRootsAndLazyChildren(EquipmentHierarchyKind kind, string label)
    {
        var page = Render<EquipmentHierarchy>();
        page.WaitForAssertion(() => Assert.Equal(5, page.FindAll("[data-node]").Count));
        Assert.Equal(5, page.FindComponents<MudTextField<string>>().Count);
        Assert.DoesNotContain("Thu gọn", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Mở nhánh", page.Markup, StringComparison.Ordinal);
        Assert.Empty(page.FindAll(".hierarchy-footer"));
        var before = _query.Requests.Count;
        page.FindComponents<MudTextField<string>>().Single(x => x.Instance.Label == label).Find("input").Input("  FILTER  ");
        page.WaitForAssertion(() => Assert.Equal(5, _query.Requests.Count - before), TimeSpan.FromSeconds(3));
        Assert.All(_query.Requests.Skip(before), request =>
        {
            var values = new[] { request.EnterpriseSearch, request.SiteSearch, request.AreaSearch, request.WorkCenterSearch, request.WorkUnitSearch };
            Assert.Equal("FILTER", values[(int)kind]);
            Assert.Single(values, x => x is not null);
            Assert.Null(request.Search);
            Assert.Equal(1, request.Page);
        });
    }

    [Fact]
    public async Task SearchFieldsCombineAndClearingOneRetainsTheOtherFilters()
    {
        var page = Render<EquipmentHierarchy>();
        page.WaitForAssertion(() => Assert.Equal(5, page.FindAll("[data-node]").Count));
        page.FindComponents<MudTextField<string>>().Single(x => x.Instance.Label == "Site").Find("input").Input("SITE");
        page.WaitForAssertion(() => Assert.Equal("SITE", _query.Requests.Last().SiteSearch), TimeSpan.FromSeconds(3));
        page.FindComponents<MudTextField<string>>().Single(x => x.Instance.Label == "Work Unit").Find("input").Input("Machine");
        page.WaitForAssertion(() => Assert.Equal("Machine", _query.Requests.Last().WorkUnitSearch), TimeSpan.FromSeconds(3));
        Assert.Equal("SITE", _query.Requests.Last().SiteSearch);
        page.WaitForAssertion(() => Assert.False(page.FindAll("button").Single(x => x.TextContent.Trim() == "Làm mới").HasAttribute("disabled")));
        await page.InvokeAsync(() => Check(page, "Chọn xóa M-001", true));
        await page.InvokeAsync(() => page.FindComponents<MudTextField<string>>().Single(x => x.Instance.Label == "Site").Find("input").Input(""));
        page.WaitForAssertion(() => Assert.Null(_query.Requests.Last().SiteSearch), TimeSpan.FromSeconds(3));
        Assert.Equal("Machine", _query.Requests.Last().WorkUnitSearch);
        page.WaitForAssertion(() => Assert.Contains("Đã chọn 0 dòng", page.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public void NativeToggleLoadsAnUnopenedBranchThroughServerDataAndCachesIt()
    {
        _query.LazySibling = true;
        var page = Render<EquipmentHierarchy>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[data-node='SITE-02']")));
        Assert.DoesNotContain(_query.Requests, x => x.ParentId == TreeQuery.LazySite.Id);
        var sibling = page.FindComponents<MudTreeViewItem<EquipmentHierarchyNode>>().Single(x => x.Instance.Value?.Code == "SITE-02");
        sibling.Find("button").Click();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[data-node='AREA-02']")));
        Assert.Single(_query.Requests, x => x.ParentId == TreeQuery.LazySite.Id && x.Page == 1 && x.PageSize == 50);
        sibling.Find("button").Click();
        page.WaitForAssertion(() => Assert.False(sibling.Instance.GetState(x => x.Expanded)));
        sibling.Find("button").Click();
        page.WaitForAssertion(() => Assert.True(sibling.Instance.GetState(x => x.Expanded)));
        Assert.Single(_query.Requests, x => x.ParentId == TreeQuery.LazySite.Id);
    }

    [Fact]
    public void NativeTreeSelectionUpdatesTheWholeAncestorPath()
    {
        var page = Render<EquipmentHierarchy>();
        page.WaitForAssertion(() => Assert.Equal(5, page.FindAll("[data-node]").Count));
        var leaf = page.FindComponents<MudTreeViewItem<EquipmentHierarchyNode>>().Single(x => x.Instance.Value?.Code == "M-001");
        leaf.Find(".mud-treeview-item-content").Click();
        page.WaitForAssertion(() => Assert.Equal("M-001", page.FindAll(".hierarchy-path-part")[^1].TextContent));
        Assert.Equal(5, page.FindAll(".hierarchy-path-part").Count);
        Assert.Contains("M-001", page.Find(".mud-treeview-item-selected").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void FailedNativeBranchLoadCanRetryWithoutLosingTheParent()
    {
        _query.FailNextBranch = true;
        var page = Render<EquipmentHierarchy>();
        page.WaitForAssertion(() => Assert.Contains("Không thể tải nhánh", page.Markup, StringComparison.Ordinal));
        page.Find("button[aria-label='Thử lại nhánh ENT-01']").Click();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[data-node='SITE-01']")));
        Assert.Single(page.FindAll("[data-node='ENT-01']"));
        Assert.DoesNotContain("Thử lại nhánh ENT-01", page.Markup, StringComparison.Ordinal);
        Assert.Equal(2, _query.Requests.Count(x => x.ParentKind == EquipmentHierarchyKind.Enterprise && x.Page == 1));
    }

    [Fact]
    public void ViewerCanExploreTreeWithoutMutationActions()
    {
        _authorization.SetRoles("Viewer");
        _authorization.SetPolicies(Capabilities.MasterDataRead);
        var page = Render<EquipmentHierarchy>();
        page.WaitForAssertion(() => Assert.Equal(5, page.FindAll("[data-node]").Count));
        Assert.Empty(page.FindAll(".hierarchy-actions"));
        Assert.DoesNotContain("Thêm Enterprise", page.Markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(EquipmentHierarchyKind.Enterprise, "Site")]
    [InlineData(EquipmentHierarchyKind.Site, "Area")]
    [InlineData(EquipmentHierarchyKind.Area, "WorkCenter")]
    [InlineData(EquipmentHierarchyKind.WorkCenter, "WorkUnit")]
    public void AddChildLocksClickedParentAndSavesThroughExistingCommand(EquipmentHierarchyKind kind, string childKind)
    {
        _ = Menus;
        var dialogs = Render<MudDialogProvider>();
        var page = Render<EquipmentHierarchy>();
        page.WaitForAssertion(() => Assert.Equal(5, page.FindAll("[data-node]").Count));
        var parent = TreeQuery.Nodes.Single(x => x.Kind == kind);
        SelectAction(page, $"Thêm con {parent.Code}", menuLabel: $"Thao tác {parent.Code}");
        dialogs.WaitForAssertion(() => Assert.Single(dialogs.FindComponents<MasterDataCreateDialog>()));
        var popup = dialogs.FindComponent<MasterDataCreateDialog>();
        Assert.Equal(parent.Id, popup.Instance.ParentId);
        Assert.True(popup.Instance.LockParent);
        if (kind == EquipmentHierarchyKind.Area)
        {
            var field = dialogs.FindComponent<MudAutocomplete<MasterLookupOption>>();
            Assert.True(field.Instance.Disabled);
            Assert.Contains("PROC-01", field.Markup, StringComparison.Ordinal);
        }
        else
        {
            var field = dialogs.FindComponent<MudSelect<Guid?>>();
            Assert.True(field.Instance.Disabled);
            Assert.Contains(parent.Code, field.Markup, StringComparison.Ordinal);
        }
        var code = dialogs.FindComponents<MudTextField<string>>().Single(x => x.Instance.Label?.StartsWith("Mã", StringComparison.Ordinal) == true);
        var name = dialogs.FindComponents<MudTextField<string>>().Single(x => x.Instance.Label?.StartsWith("Tên", StringComparison.Ordinal) == true);
        code.Find("input").Change("NEW-CHILD");
        name.Find("input").Change("New child");
        dialogs.Find("textarea").Change("Mô tả con mới");
        dialogs.WaitForAssertion(() =>
        {
            var save = dialogs.FindAll("button").Single(x => x.TextContent.Trim() == "Lưu");
            Assert.False(save.HasAttribute("disabled"));
            save.Click();
        });
        page.WaitForAssertion(() => Assert.Equal(childKind, _commands.LastHierarchyCreatedKind));
        Assert.Equal(parent.Id, _commands.LastHierarchyParentId);
        Assert.Equal("Mô tả con mới", _commands.LastHierarchyDescription);
        page.WaitForAssertion(() => Assert.True(_query.Requests.Count(x => x.ParentKind is null) >= 2));
    }

    [Fact]
    public void DeleteRequiresConfirmationAndPassesTheDisplayedConcurrencyToken()
    {
        var dialogs = Render<MudDialogProvider>();
        var page = Render<EquipmentHierarchy>();
        page.WaitForAssertion(() => Assert.Equal(5, page.FindAll("[data-node]").Count));
        SelectAction(page, "Xóa M-001", menuLabel: "Thao tác M-001");
        dialogs.WaitForAssertion(() => Assert.Single(dialogs.FindAll(".mud-dialog-title")));
        Assert.Null(_commands.LastDeletedWorkUnit);
        dialogs.FindAll("button").Single(x => x.TextContent.Trim() == "Hủy").Click();
        Assert.Null(_commands.LastDeletedWorkUnit);
        SelectAction(page, "Xóa M-001", menuLabel: "Thao tác M-001");
        dialogs.WaitForAssertion(() => Assert.Contains("Xóa Work Unit", dialogs.Markup, StringComparison.Ordinal));
        dialogs.FindAll("button").Single(x => x.TextContent.Trim() == "Xóa").Click();
        page.WaitForAssertion(() => Assert.NotNull(_commands.LastDeletedWorkUnit));
        var unit = TreeQuery.Nodes.Single(x => x.Kind == EquipmentHierarchyKind.WorkUnit);
        Assert.Equal((unit.Id, unit.RowVersion), _commands.LastDeletedWorkUnit.GetValueOrDefault());
    }

    [Fact]
    public void BulkDeleteRequiresConfirmationAndSendsOnlyCheckedRows()
    {
        var dialogs = Render<MudDialogProvider>();
        var page = Render<EquipmentHierarchy>();
        page.WaitForAssertion(() => Assert.Equal(5, page.FindAll("[data-node]").Count));
        var path = page.Find(".hierarchy-path").TextContent;
        Check(page, "Chọn xóa M-001", true);
        Check(page, "Chọn xóa LINE-01", true);
        page.WaitForAssertion(() => Assert.Contains("Đã chọn 2 dòng", page.Markup, StringComparison.Ordinal));
        Assert.Equal(path, page.Find(".hierarchy-path").TextContent);
        page.FindAll("button").Single(x => x.TextContent.Trim() == "Xóa đã chọn (2)").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Xóa 2 dòng", dialogs.Markup, StringComparison.Ordinal));
        Assert.Null(_commands.LastBulkDelete);
        dialogs.FindAll("button").Single(x => x.TextContent.Trim() == "Hủy").Click();
        Assert.Null(_commands.LastBulkDelete);
        page.FindAll("button").Single(x => x.TextContent.Trim() == "Xóa đã chọn (2)").Click();
        dialogs.WaitForAssertion(() => Assert.Single(dialogs.FindAll(".mud-dialog-title")));
        dialogs.FindAll("button").Single(x => x.TextContent.Trim() == "Xóa đã chọn").Click();
        page.WaitForAssertion(() => Assert.Equal(2, _commands.LastBulkDelete?.Length));
        Assert.All(_commands.LastBulkDelete!, item =>
        {
            var node = TreeQuery.Nodes.Single(x => x.Id == item.Id);
            Assert.Equal(node.Kind, item.Kind);
            Assert.Equal(node.RowVersion, item.RowVersion);
        });
        Assert.Contains(_commands.LastBulkDelete!, x => x.Kind == EquipmentHierarchyKind.WorkUnit);
        Assert.Contains(_commands.LastBulkDelete!, x => x.Kind == EquipmentHierarchyKind.WorkCenter);
        page.WaitForAssertion(() => Assert.Contains("Đã chọn 0 dòng", page.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public void SelectAllUsesOnlyVisibleRowsAndSearchClearsTheSelection()
    {
        var page = Render<EquipmentHierarchy>();
        page.WaitForAssertion(() => Assert.Equal(5, page.FindAll("[data-node]").Count));
        var root = page.FindComponents<MudTreeViewItem<EquipmentHierarchyNode>>().Single(x => x.Instance.Value?.Code == "ENT-01");
        root.Find("button").Click();
        page.WaitForAssertion(() => Assert.False(root.Instance.GetState(x => x.Expanded)));
        Check(page, "Chọn tất cả dòng đang hiển thị", true);
        Assert.Contains("Đã chọn 1 dòng", page.Markup, StringComparison.Ordinal);
        page.Find("input").Input("LATEST");
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[data-node='LATEST']")), TimeSpan.FromSeconds(3));
        Assert.Contains("Đã chọn 0 dòng", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void FailedBulkDeleteKeepsSelectionAndDoesNotExposeTechnicalErrors()
    {
        _commands.FailBulkDelete = true;
        var dialogs = Render<MudDialogProvider>();
        var page = Render<EquipmentHierarchy>();
        page.WaitForAssertion(() => Assert.Equal(5, page.FindAll("[data-node]").Count));
        Check(page, "Chọn xóa M-001", true);
        page.FindAll("button").Single(x => x.TextContent.Trim() == "Xóa đã chọn (1)").Click();
        dialogs.WaitForAssertion(() => Assert.Single(dialogs.FindAll(".mud-dialog-title")));
        dialogs.FindAll("button").Single(x => x.TextContent.Trim() == "Xóa đã chọn").Click();
        page.WaitForAssertion(() => Assert.Contains("toàn bộ lượt xóa đã được hủy", page.Markup, StringComparison.Ordinal));
        Assert.Contains("Đã chọn 1 dòng", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("password=hidden", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void ViewerHasNoBulkCheckboxesOrDeleteAction()
    {
        _authorization.SetRoles("Viewer");
        _authorization.SetPolicies(Capabilities.MasterDataRead);
        var page = Render<EquipmentHierarchy>();
        page.WaitForAssertion(() => Assert.Equal(5, page.FindAll("[data-node]").Count));
        Assert.Empty(page.FindComponents<MudCheckBox<bool>>());
        Assert.DoesNotContain("Xóa đã chọn", page.Markup, StringComparison.Ordinal);
    }

    private static void Check(IRenderedComponent<EquipmentHierarchy> page, string label, bool value)
    {
        var element = page.Find($"[aria-label='{label}']");
        (element.QuerySelector("input") ?? element).Change(value);
    }

    [Fact]
    public void QueryFailureShowsRetryAndDoesNotExposeInfrastructureDetails()
    {
        _query.Fail = true;
        var page = Render<EquipmentHierarchy>();
        page.WaitForAssertion(() => Assert.Contains("Không thể tải cây", page.Markup, StringComparison.Ordinal));
        Assert.DoesNotContain("password=hidden", page.Markup, StringComparison.Ordinal);
        _query.Fail = false;
        page.FindAll("button").Single(x => x.TextContent.Trim() == "Làm mới").Click();
        page.WaitForAssertion(() => Assert.Contains("ENT-01", page.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public void LargeBranchLoadsNextPageWithoutHidingTheAlreadyExpandedRows()
    {
        _query.LargeBranch = true;
        var page = Render<EquipmentHierarchy>();
        page.WaitForAssertion(() => Assert.Equal(54, page.FindAll("[data-node]").Count));
        page.Find("button[aria-label='Xem thêm con LINE-01']").Click();
        page.WaitForAssertion(() => Assert.Equal(59, page.FindAll("[data-node]").Count));
        Assert.Single(page.FindAll("[data-node='M-055']"));
        Assert.Single(page.FindAll("[data-node='M-001']"));
        Assert.Contains(_query.Requests, x => x.ParentKind == EquipmentHierarchyKind.WorkCenter && x.Page == 2 && x.PageSize == 50);
        Assert.DoesNotContain("Xem thêm con LINE-01", page.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LateSearchResponseCannotOverwriteTheCurrentTree()
    {
        _query.DelayInitialRoot = true;
        var page = Render<EquipmentHierarchy>();
        page.Find("input").Input("LATEST");
        page.WaitForAssertion(() => Assert.Single(page.FindAll("[data-node='LATEST']")), TimeSpan.FromSeconds(3));
        _query.InitialRoot.SetResult(new([TreeQuery.Nodes[0]], 1));
        await page.InvokeAsync(() => Task.CompletedTask);
        page.WaitForAssertion(() =>
        {
            Assert.Single(page.FindAll("[data-node='LATEST']"));
            Assert.Empty(page.FindAll("[data-node='ENT-01']"));
        });
    }

    private static int NativeDepth(AngleSharp.Dom.IElement node)
    {
        var ancestors = 0;
        for (var parent = node.ParentElement; parent is not null; parent = parent.ParentElement)
            if (parent.ClassList.Contains("mud-treeview-item")) ancestors++;
        return ancestors - 1;
    }

    private sealed class TreeQuery : IEquipmentHierarchyQueryService
    {
        public bool Fail { get; set; }
        public bool FailNextBranch { get; set; }
        public bool LazySibling { get; set; }
        public bool LargeBranch { get; set; }
        public bool DelayInitialRoot { get; set; }
        public TaskCompletionSource<PagedResult<EquipmentHierarchyNode>> InitialRoot { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<EquipmentHierarchyQuery> Requests { get; } = [];
        public static EquipmentHierarchyNode[] Nodes { get; } = Enumerable.Range(0, 5).Select(level =>
            new EquipmentHierarchyNode(
                Guid.Parse($"{level + 1}0000000-0000-0000-0000-000000000001"),
                level == 0 ? null : Guid.Parse($"{level}0000000-0000-0000-0000-000000000001"),
                (EquipmentHierarchyKind)level,
                new[] { "ENT-01", "SITE-01", "PROC-01", "LINE-01", "M-001" }[level],
                new[] { "Enterprise", "Factory", "Area", "Line", "Machine" }[level],
                level == 4 ? 0 : 1, "Active", new DateTimeOffset(2026, 10, 10, 0, 0, 0, TimeSpan.Zero),
                Convert.ToBase64String([1, 2]), level == 4 ? "Running" : null, level == 4 ? "Connected" : null,
                $"Mô tả {(EquipmentHierarchyKind)level}")).ToArray();
        public static EquipmentHierarchyNode LazySite { get; } = Nodes[1] with { Id = Guid.Parse("20000000-0000-0000-0000-000000000002"), Code = "SITE-02" };

        public Task<PagedResult<EquipmentHierarchyNode>> GetAsync(EquipmentHierarchyQuery query, CancellationToken cancellationToken)
        {
            Requests.Add(query);
            if (query.ParentKind is not null && FailNextBranch)
            {
                FailNextBranch = false;
                throw new InvalidOperationException("branch unavailable");
            }
            if (Fail) return Task.FromException<PagedResult<EquipmentHierarchyNode>>(new InvalidOperationException("password=hidden"));
            if (DelayInitialRoot && Requests.Count == 1 && query.ParentKind is null) return InitialRoot.Task;
            if (query.EnterpriseSearch == "LATEST") return Task.FromResult(new PagedResult<EquipmentHierarchyNode>(
                [Nodes[0] with { Code = "LATEST", Name = "Latest result", ChildCount = 0 }], 1));
            if (LazySibling && query.ParentKind == EquipmentHierarchyKind.Enterprise)
                return Task.FromResult(new PagedResult<EquipmentHierarchyNode>([Nodes[1], LazySite], 2));
            if (LazySibling && query.ParentId == LazySite.Id)
                return Task.FromResult(new PagedResult<EquipmentHierarchyNode>(
                    [Nodes[2] with { Id = Guid.Parse("30000000-0000-0000-0000-000000000002"), ParentId = LazySite.Id, Code = "AREA-02", ChildCount = 0 }], 1));
            if (LargeBranch && query.ParentKind == EquipmentHierarchyKind.WorkCenter)
            {
                var units = Enumerable.Range(1, 55).Select(index => Nodes[4] with
                {
                    Id = Guid.Parse($"50000000-0000-0000-0000-{index:D12}"), Code = $"M-{index:D3}",
                }).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArray();
                return Task.FromResult(new PagedResult<EquipmentHierarchyNode>(units, 55));
            }
            var nodes = Nodes.Where(x => query.ParentKind is null ? x.Kind == EquipmentHierarchyKind.Enterprise
                : x.Kind == query.ParentKind + 1 && x.ParentId == query.ParentId).ToArray();
            return Task.FromResult(new PagedResult<EquipmentHierarchyNode>(nodes, nodes.Length));
        }
    }
}
