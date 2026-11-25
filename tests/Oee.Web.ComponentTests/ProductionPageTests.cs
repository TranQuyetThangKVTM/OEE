using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.Operations;
using Oee.Application.Modules;
using Oee.Application.Security;
using Oee.Domain.Common;
using Oee.Web.Components.Pages.Production;
using Oee.Web.Components.Shared;

namespace Oee.Web.ComponentTests;

/// <summary>
/// bUnit Test cho danh sách kế hoạch, downtime, reconciliation và ba dialog bulk Production.
/// </summary>
public sealed class ProductionPageTests : RowMenuTestContext, IAsyncLifetime
{
    private static readonly Guid WorkUnitId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid ProductId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid ShiftId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid ReasonId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 10, 0, 0, TimeSpan.Zero);

    /// <summary>Khởi tạo PlanningManager để kiểm tra các action có ProductionPlanManage.</summary>
    public ProductionPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddScoped<ProductionPlanCreationRunner>();
        Services.AddScoped<Oee.Web.Security.ScopedPrincipalAccessor>();
        Services.AddAuthorizationCore(options =>
        {
            foreach (var capability in new[] { Capabilities.ProductionPlanManage, Capabilities.DowntimeWrite })
                options.AddPolicy(capability, policy => policy.RequireRole("Admin", "PlanningManager"));
        });
        var authorization = this.AddAuthorization();
        authorization.SetAuthorized("planner");
        authorization.SetRoles("PlanningManager");
        authorization.SetPolicies(Capabilities.ProductionPlanManage, Capabilities.DowntimeWrite);
    }

    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;
    async Task IAsyncLifetime.DisposeAsync() => await base.DisposeAsync();

    [Fact]
    public void PlanListRendersApplicationResultAndUsesServerPaging()
    {
        // Arrange: fake query ghi lại request phân trang và trả một kế hoạch đã có output.
        var query = new FakeProductionQuery();
        Services.AddScoped<IProductionModuleQueryService>(_ => query);
        Services.AddScoped<IProductionPlanScenario>(_ => new NoOpProductionPlanService()).AddScenarioRequests();

        // Act: render danh sách kế hoạch qua service contract.
        var component = Render<ProductionPlans>();

        // Assert: row/action correction hiển thị và request đầu tiên dùng page 1, size 25 phía server.
        component.WaitForAssertion(() =>
        {
            Assert.Contains("PLAN-001", component.Markup, StringComparison.Ordinal);
            Assert.Contains("M-001", component.Markup, StringComparison.Ordinal);
            Assert.Equal("Blow Moulder", component.Find("td[data-label='Thiết bị'] .table-subline").TextContent);
            Assert.Equal("Wakeup 247", component.Find("td[data-label='Sản phẩm'] .table-subline").TextContent);
            Assert.Single(component.FindAll(".row-action-menu"));
            Assert.NotNull(query.LastPlanQuery);
            Assert.Equal(1, query.LastPlanQuery.Page);
            Assert.Equal(25, query.LastPlanQuery.PageSize);
        });
        OpenActions(component);
        Assert.NotNull(MenuAction("Sửa kế hoạch"));
    }

    [Fact]
    public void PlanQueryFailureKeepsPageHeaderRefreshWithoutTechnicalErrorBanner()
    {
        // Arrange: query kế hoạch ném lỗi database.
        Services.AddScoped<IProductionModuleQueryService>(_ => new FakeProductionQuery(failPlans: true));
        Services.AddScoped<IProductionPlanScenario>(_ => new NoOpProductionPlanService()).AddScenarioRequests();

        var component = Render<ProductionPlans>();

        // Assert: lỗi SQL không đưa ra UI; thao tác tải lại nằm trong PageHeader.
        component.WaitForAssertion(() =>
        {
            Assert.Contains("Làm mới", component.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("database unavailable", component.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void PlansOnlyOfferEditDeleteAndDeleteNeedsConfirmationWithoutReason()
    {
        var query = new FakeProductionQuery();
        var service = new NoOpProductionPlanService { OnDeleted = () => query.PlanDeleted = true };
        Services.AddScoped<IProductionModuleQueryService>(_ => query);
        Services.AddScoped<IProductionPlanScenario>(_ => service).AddScenarioRequests();
        var dialogs = Render<MudDialogProvider>();
        var page = Render<ProductionPlans>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".row-action-menu")));
        Assert.Empty(page.FindAll("button[aria-label='Phát hành kế hoạch']"));
        Assert.Empty(page.FindAll("a[aria-label='Xem kế hoạch']"));
        Assert.Contains("Đang thực hiện", page.Markup, StringComparison.Ordinal);
        SelectAction(page, "Xóa kế hoạch");
        dialogs.WaitForAssertion(() => Assert.Contains("Xóa kế hoạch PLAN-001?", dialogs.Markup, StringComparison.Ordinal));
        Assert.Empty(dialogs.FindAll("input,textarea"));
        dialogs.FindAll("button").Single(x => x.TextContent.Trim() == "Hủy").Click();
        Assert.Equal(0, service.DeleteCalls);
        SelectAction(page, "Xóa kế hoạch");
        dialogs.WaitForAssertion(() => Assert.Contains("Xóa kế hoạch PLAN-001?", dialogs.Markup, StringComparison.Ordinal));
        dialogs.FindAll("button").Single(x => x.TextContent.Trim() == "Xóa").Click();
        page.WaitForAssertion(() =>
        {
            Assert.Equal(1, service.DeleteCalls);
            Assert.Empty(page.FindAll(".row-action-menu"));
        });
    }

    [Theory]
    [InlineData(ProductionPlanExecutionStatusEnum.Pending, "Chưa bắt đầu", 0)]
    [InlineData(ProductionPlanExecutionStatusEnum.InProgress, "Đang thực hiện", 600)]
    [InlineData(ProductionPlanExecutionStatusEnum.MissingOutput, "Chưa ghi nhận", 0)]
    [InlineData(ProductionPlanExecutionStatusEnum.CompletedUnderTarget, "Hụt KH", 600)]
    [InlineData(ProductionPlanExecutionStatusEnum.CompletedOnTarget, "Đạt KH", 1500)]
    public void PlanListUsesExecutionStatusAndCombinesActualWithUncappedAttainment(
        ProductionPlanExecutionStatusEnum status, string label, int actual)
    {
        Services.AddScoped<IProductionModuleQueryService>(_ => new FakeProductionQuery
        {
            ExecutionStatus = status, ActualQuantity = actual
        });
        Services.AddScoped<IProductionPlanScenario>(_ => new NoOpProductionPlanService()).AddScenarioRequests();
        var page = Render<ProductionPlans>();
        page.WaitForAssertion(() => Assert.Contains(label, page.Markup, StringComparison.Ordinal));
        Assert.DoesNotContain("Có hiệu lực", page.Markup, StringComparison.Ordinal);
        Assert.Empty(page.FindAll("td[data-label='Thời gian'],td[data-label='Thực tế']"));
        var attainment = page.Find("td[data-label='Đạt KH']");
        Assert.Equal(actual.ToString("N0", System.Globalization.CultureInfo.CurrentCulture), attainment.QuerySelector("strong")!.TextContent);
        Assert.Contains((actual / 1200m).ToString("P1", System.Globalization.CultureInfo.CurrentCulture), attainment.TextContent, StringComparison.Ordinal);
        var shift = page.Find("td[data-label='Ca']").TextContent;
        Assert.Contains("S1", shift, StringComparison.Ordinal);
        Assert.Contains(Now.AddHours(-1).ToLocalTime().ToString("dd/MM/yyyy", System.Globalization.CultureInfo.CurrentCulture), shift, StringComparison.Ordinal);
        Assert.Contains(Now.AddHours(7).ToLocalTime().ToString("HH:mm", System.Globalization.CultureInfo.CurrentCulture), shift, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EditOpensMudDialogWithSeparateDateTimeFieldsAndSavesUtc(bool hasOutput)
    {
        Services.AddScoped<IProductionModuleQueryService>(_ => new FakeProductionQuery());
        var service = new NoOpProductionPlanService { HasOutput = hasOutput };
        Services.AddScoped<IProductionPlanScenario>(_ => service).AddScenarioRequests();
        var dialogs = Render<MudDialogProvider>();
        var page = Render<ProductionPlans>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".row-action-menu")));
        SelectAction(page, "Sửa kế hoạch");
        dialogs.WaitForAssertion(() => Assert.Contains("Cập nhật kế hoạch", dialogs.Markup, StringComparison.Ordinal));
        dialogs.WaitForAssertion(() => Assert.Contains("Mã kế hoạch (tự sinh)", dialogs.Markup, StringComparison.Ordinal));
        var editor = dialogs.FindComponent<ProductionPlanEditDialog>();
        Assert.Equal(2, editor.FindComponents<MudDatePicker>().Count);
        Assert.Equal(2, editor.FindComponents<MudTimePicker>().Count);
        Assert.Empty(editor.FindAll("select,input[type='datetime-local']"));
        dialogs.FindAll("button").Single(x => x.TextContent.Trim() == "Lưu").Click();
        page.WaitForAssertion(() => Assert.NotNull(service.SavedRequest));
        Assert.Equal(Now, service.SavedRequest!.PlannedStart);
        Assert.Equal(Now.AddHours(1), service.SavedRequest.PlannedEnd);
        Assert.Equal(TimeSpan.Zero, service.SavedRequest.PlannedStart.Offset);
        Assert.Equal(hasOutput ? "PLAN-001-V2" : "PLAN-001", service.SavedRequest.Code);
        Assert.Equal(hasOutput ? 1 : 0, service.CorrectionCalls);
        Assert.Equal(hasOutput ? 0 : 1, service.UpdateCalls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EditNightShiftUpdatesCodeAndRollsEndDateForward(bool hasOutput)
    {
        Services.AddScoped<IProductionModuleQueryService>(_ => new FakeProductionQuery());
        var service = new NoOpProductionPlanService { HasOutput = hasOutput };
        Services.AddScoped<IProductionPlanScenario>(_ => service).AddScenarioRequests();
        var dialogs = Render<MudDialogProvider>();
        var page = Render<ProductionPlans>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".row-action-menu")));
        SelectAction(page, "Sửa kế hoạch");
        dialogs.WaitForAssertion(() => Assert.Contains("Mã kế hoạch (tự sinh)", dialogs.Markup, StringComparison.Ordinal));
        var editor = dialogs.FindComponent<ProductionPlanEditDialog>();
        await editor.InvokeAsync(() => editor.FindComponent<MudSelect<Guid?>>().Instance.ValueChanged.InvokeAsync(ShiftId));
        await editor.InvokeAsync(() => editor.FindComponents<MudDatePicker>()[0].Instance.DateChanged.InvokeAsync(new DateTime(2026, 8, 3)));
        dialogs.FindAll("button").Single(x => x.TextContent.Trim() == "Lưu").Click();
        page.WaitForAssertion(() => Assert.NotNull(service.SavedRequest));
        Assert.Equal(hasOutput ? "SKU-001-20260803-S1-BlowMould-V2" : "SKU-001-20260803-S1-BlowMoulder", service.SavedRequest!.Code);
        Assert.Equal(new DateTimeOffset(2026, 8, 3, 15, 0, 0, TimeSpan.Zero), service.SavedRequest.PlannedStart);
        Assert.Equal(new DateTimeOffset(2026, 8, 3, 23, 0, 0, TimeSpan.Zero), service.SavedRequest.PlannedEnd);
    }

    [Fact]
    public async Task EditingDateWithAnOversizedSkuShowsAnErrorInsteadOfCrashingTheDialog()
    {
        Services.AddScoped<IProductionModuleQueryService>(_ => new FakeProductionQuery { LookupProductCode = new string('S', 32) });
        var service = new NoOpProductionPlanService();
        Services.AddScoped<IProductionPlanScenario>(_ => service).AddScenarioRequests();
        var dialogs = Render<MudDialogProvider>();
        var page = Render<ProductionPlans>();
        SelectAction(page, "Sửa kế hoạch");
        dialogs.WaitForAssertion(() => Assert.Contains("Mã kế hoạch (tự sinh)", dialogs.Markup, StringComparison.Ordinal));
        var editor = dialogs.FindComponent<ProductionPlanEditDialog>();
        await editor.InvokeAsync(() => editor.FindComponents<MudDatePicker>()[0].Instance.DateChanged.InvokeAsync(new DateTime(2026, 8, 3)));

        dialogs.FindAll("button").Single(x => x.TextContent.Trim() == "Lưu").Click();

        dialogs.WaitForAssertion(() => Assert.Contains("Mã SKU hoặc mã ca quá dài", dialogs.Markup, StringComparison.Ordinal));
        Assert.Null(service.SavedRequest);
        Assert.Single(dialogs.FindComponents<ProductionPlanEditDialog>());
    }
    [Fact]
    public void DowntimeListShowsOpenUnknownEventAndServerStatistics()
    {
        // Arrange & Act: fake query trả downtime DR-UNKNOWN với EndAt null.
        Services.AddScoped<IProductionModuleQueryService>(_ => new FakeProductionQuery());
        Services.AddScoped<IDowntimeScenario>(_ => new NoOpDowntimeService()).AddScenarioRequests();

        var component = Render<DowntimeEvents>();

        // Assert: UI nhận diện event đang mở, hiển thị detail và KPI phân loại từ server.
        component.WaitForAssertion(() =>
        {
            Assert.Contains("DR-UNKNOWN", component.Markup, StringComparison.Ordinal);
            Assert.Contains("Đang mở", component.Markup, StringComparison.Ordinal);
            Assert.Contains("100.0", component.Markup, StringComparison.Ordinal);
        });
        OpenActions(component);
        Assert.NotNull(MenuAction("Chi tiết downtime"));
    }

    [Fact]
    public void ProductionAndSampleButtonsOpenTheFourSeparateMudDialogs()
    {
        // Arrange: đăng ký query/command contract và các provider MudBlazor thật.
        Services.AddScoped<IProductionModuleQueryService>(_ => new FakeProductionQuery());
        Services.AddScoped<IProductionPlanScenario>(_ => new NoOpProductionPlanService()).AddScenarioRequests();
        Services.AddScoped<ISampleProductionPlanScenario>(_ => new NoOpSamplePlanService()).AddScenarioRequests();
        _ = Menus;
        var dialogs = Render<MudDialogProvider>();

        // Act & Assert: nút Tạo kế hoạch chỉ mở form trực tiếp.
        var plans = Render<ProductionPlans>();
        plans.WaitForAssertion(() => Assert.Contains("PLAN-001", plans.Markup, StringComparison.Ordinal));
        plans.FindAll("button").Single(button => button.TextContent.Trim() == "Tạo kế hoạch").Click();
        dialogs.WaitForAssertion(() =>
        {
            Assert.Contains("Thêm kế hoạch sản xuất", dialogs.Markup, StringComparison.Ordinal);
            Assert.Single(dialogs.FindComponents<ProductionPlanCreateDialog>());
            Assert.Empty(dialogs.FindComponents<ProductionPlanFromSampleDialog>());
            Assert.Empty(dialogs.FindComponents<MudTabs>());
        });
        dialogs.FindAll("button").Single(button => button.TextContent.Trim() == "Hủy").Click();

        plans.FindAll("button").Single(button => button.TextContent.Trim() == "Từ kế hoạch mẫu").Click();
        dialogs.WaitForAssertion(() =>
        {
            Assert.Contains("Tạo kế hoạch từ mẫu", dialogs.Markup, StringComparison.Ordinal);
            Assert.Single(dialogs.FindComponents<ProductionPlanFromSampleDialog>());
            Assert.Empty(dialogs.FindComponents<ProductionPlanCreateDialog>());
            Assert.Empty(dialogs.FindComponents<MudTabs>());
        });
        dialogs.FindAll("button").Single(button => button.TextContent.Trim() == "Hủy").Click();

        // Act & Assert: nút Thêm mẫu mở dialog cha với quantity cho mỗi dòng.
        var samples = Render<SamplePlans>();
        samples.WaitForAssertion(() => Assert.Contains("SAMPLE-001", samples.Markup, StringComparison.Ordinal));
        samples.FindAll("button").Single(button => button.TextContent.Trim() == "Thêm mẫu").Click();
        dialogs.WaitForAssertion(() =>
        {
            Assert.Contains("Thêm kế hoạch mẫu", dialogs.Markup, StringComparison.Ordinal);
            Assert.Contains("Tên kế hoạch mẫu", dialogs.Markup, StringComparison.Ordinal);
            Assert.Contains("Sản lượng kế hoạch mỗi dòng", dialogs.Markup, StringComparison.Ordinal);
        });
        dialogs.FindAll("button").Single(button => button.TextContent.Trim() == "Hủy").Click();

        // Act & Assert: trang chi tiết mở dialog thêm nhiều dòng vào mẫu đang tồn tại.
        var detail = Render<Oee.Web.Components.Pages.Production.SamplePlanDetail>(parameters =>
            parameters.Add(x => x.Id, Guid.Parse("70000000-0000-0000-0000-000000000001")));
        detail.WaitForAssertion(() => Assert.Contains("Thêm dòng", detail.Markup, StringComparison.Ordinal));
        detail.FindAll("button").Single(button => button.TextContent.Trim() == "Thêm dòng").Click();
        dialogs.WaitForAssertion(() =>
        {
            Assert.Contains("Thêm dòng vào mẫu", dialogs.Markup, StringComparison.Ordinal);
            Assert.Contains("Dòng tối đa", dialogs.Markup, StringComparison.Ordinal);
            Assert.Contains("Xem trước dòng mới", dialogs.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void SampleListOnlyOffersDetailCopyAndDelete()
    {
        Services.AddScoped<IProductionModuleQueryService>(_ => new FakeProductionQuery());
        Services.AddScoped<ISampleProductionPlanScenario>(_ => new NoOpSamplePlanService()).AddScenarioRequests();
        var page = Render<SamplePlans>();
        OpenActions(page);
        Assert.NotNull(MenuAction("Sao chép mẫu"));
        Assert.NotNull(MenuAction("Xóa mẫu"));
        Assert.Empty(Menus.FindAll("[aria-label='Sửa mẫu']"));
        Assert.Contains("/production/plan-templates/", MenuAction("Mở chi tiết mẫu").GetAttribute("href"), StringComparison.Ordinal);
    }

    [Fact]
    public void SampleDetailGroupsMachineRowsAndAllowsSelectingOrCollapsingOneMachine()
    {
        var source = SampleRows();
        var query = new FakeProductionQuery
        {
            SampleRows =
            [
                source[0], source[1],
                source[0] with { Id = Guid.NewGuid(), WorkUnitId = Guid.NewGuid(), WorkUnit = "M-002" },
                source[0] with { Id = Guid.NewGuid() }
            ]
        };
        Services.AddScoped<IProductionModuleQueryService>(_ => query);
        Services.AddScoped<ISampleProductionPlanScenario>(_ => new NoOpSamplePlanService()).AddScenarioRequests();

        var page = Render<Oee.Web.Components.Pages.Production.SamplePlanDetail>(p => p.Add(x => x.Id, Guid.NewGuid()));

        page.WaitForAssertion(() =>
        {
            Assert.Equal(2, page.FindAll(".sample-machine-group").Count);
            Assert.Equal(4, page.FindAll("tr.sample-machine-line").Count);
            Assert.Equal(2, page.FindAll(".sample-machine-code").Count);
            Assert.Contains("3 dòng trên trang này", page.FindAll(".sample-machine-summary")[0].TextContent, StringComparison.Ordinal);
            Assert.False(page.FindComponent<MudTable<SamplePlanItemDetail>>().Instance.Striped);
            Assert.Equal(7, page.FindAll("colgroup col").Count);
            Assert.Contains("36px", page.Find("colgroup col").GetAttribute("style"), StringComparison.Ordinal);
            Assert.All(page.FindAll("tr.sample-machine-line"), row => Assert.Equal(7, row.Children.Length));
        });

        page.FindComponents<MudCheckBox<bool>>().Single(box => box.Markup.Contains("Chọn nhóm M-001", StringComparison.Ordinal))
            .Find("input").Change(true);
        page.WaitForAssertion(() => Assert.Contains("Xóa đã chọn (3)", page.Markup, StringComparison.Ordinal));
        page.FindAll(".sample-machine-group")[0].QuerySelector("button")!.Click();
        page.WaitForAssertion(() => Assert.Single(page.FindAll("tr.sample-machine-line")));
        Assert.Equal(2, page.FindAll(".sample-machine-group").Count);
    }

    [Fact]
    public async Task SampleDetailCanSwitchSkuGroupingSelectAcrossMachinesAndReturnToFlatRows()
    {
        var rows = SampleRows();
        var query = new FakeProductionQuery { SampleRows =
        [
            rows[0], rows[1],
            rows[0] with { Id = Guid.NewGuid(), WorkUnitId = Guid.NewGuid(), WorkUnit = "M-002" },
            rows[0] with { Id = Guid.NewGuid(), ProductId = Guid.NewGuid(), Product = "SKU-002" }
        ] };
        Services.AddScoped<IProductionModuleQueryService>(_ => query);
        Services.AddScoped<ISampleProductionPlanScenario>(_ => new NoOpSamplePlanService()).AddScenarioRequests();
        var page = Render<Oee.Web.Components.Pages.Production.SamplePlanDetail>(p => p.Add(x => x.Id, Guid.NewGuid()));
        page.WaitForAssertion(() => Assert.Equal(4, page.FindAll("tr.sample-machine-line").Count));
        var selector = page.FindComponent<TableGroupingSelector>();

        await page.InvokeAsync(() => selector.Instance.ValueChanged.InvokeAsync(TableGroupingMode.Product));
        page.WaitForAssertion(() =>
        {
            Assert.Equal(2, page.FindAll(".sample-machine-group").Count);
            Assert.Equal("SKU-001", page.FindAll(".sample-machine-code")[0].TextContent);
            Assert.Contains("M-002", page.Find("tbody").TextContent, StringComparison.Ordinal);
            Assert.All(page.FindAll("tr.sample-machine-line"), row => Assert.Equal(7, row.Children.Length));
        });
        page.FindComponents<MudCheckBox<bool>>().Single(box => box.Markup.Contains("Chọn nhóm SKU-001", StringComparison.Ordinal))
            .Find("input").Change(true);
        page.WaitForAssertion(() => Assert.Contains("Xóa đã chọn (3)", page.Markup, StringComparison.Ordinal));
        page.Find("button[aria-label='Thu gọn tất cả nhóm']").Click();
        page.WaitForAssertion(() => Assert.Empty(page.FindAll("tr.sample-machine-line")));
        page.Find("button[aria-label='Mở tất cả nhóm']").Click();
        page.WaitForAssertion(() => Assert.Equal(4, page.FindAll("tr.sample-machine-line").Count));

        await page.InvokeAsync(() => selector.Instance.ValueChanged.InvokeAsync(TableGroupingMode.None));
        page.WaitForAssertion(() =>
        {
            Assert.Empty(page.FindAll(".sample-machine-group"));
            Assert.Equal(4, page.FindAll("tr.sample-machine-line").Count);
            Assert.Contains("Xóa đã chọn (0)", page.Markup, StringComparison.Ordinal);
            Assert.Equal(7, page.FindAll("colgroup col").Count);
        });
    }

    [Fact]
    public async Task ProductionPlanGroupingDoesNotChangeTheServerQueryOrAttainment()
    {
        var query = new FakeProductionQuery();
        Services.AddScoped<IProductionModuleQueryService>(_ => query);
        Services.AddScoped<IProductionPlanScenario>(_ => new NoOpProductionPlanService()).AddScenarioRequests();
        var page = Render<ProductionPlans>();
        page.WaitForAssertion(() => Assert.NotNull(query.LastPlanQuery));
        var before = query.LastPlanQuery;
        var selector = page.FindComponent<TableGroupingSelector>();
        await page.InvokeAsync(() => selector.Instance.ValueChanged.InvokeAsync(TableGroupingMode.Product));
        page.WaitForAssertion(() => Assert.Contains("SKU-001", page.Find(".oee-table-group").TextContent, StringComparison.Ordinal));
        Assert.Equal(before, query.LastPlanQuery);
        Assert.Contains("600", page.Find("tbody").TextContent, StringComparison.Ordinal);
        await page.InvokeAsync(() => selector.Instance.ValueChanged.InvokeAsync(TableGroupingMode.None));
        page.WaitForAssertion(() => Assert.Empty(page.FindAll(".oee-table-group")));
    }

    [Fact]
    public void SampleDetailSelectsVisibleRowsAndConfirmsBeforeDeleting()
    {
        var rows = SampleRows();
        var query = new FakeProductionQuery { SampleRows = rows };
        var commands = new NoOpSamplePlanService { OnItemDeleted = () => query.SampleRows = [] };
        Services.AddScoped<IProductionModuleQueryService>(_ => query);
        Services.AddScoped<ISampleProductionPlanScenario>(_ => commands).AddScenarioRequests();
        var dialogs = Render<MudDialogProvider>();
        var page = Render<Oee.Web.Components.Pages.Production.SamplePlanDetail>(p => p.Add(x => x.Id, Guid.NewGuid()));
        page.WaitForAssertion(() => Assert.Equal(2, page.FindComponents<MudCheckBox<bool>>().Count(box => box.Markup.Contains("Chọn dòng ", StringComparison.Ordinal))));
        Assert.Contains("Blow Moulder", page.Markup, StringComparison.Ordinal);
        Assert.Contains("22:00–06:00", page.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Phân bổ kế hoạch", page.Markup, StringComparison.Ordinal);
        Assert.Empty(page.FindAll("select,input[type='date']"));
        page.FindComponents<MudCheckBox<bool>>().Single(box => box.Markup.Contains("Chọn tất cả dòng đang hiển thị", StringComparison.Ordinal)).Find("input").Change(true);
        page.FindAll("button").Single(x => x.TextContent.Trim() == "Xóa đã chọn (2)").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Xóa 2 dòng", dialogs.Markup, StringComparison.Ordinal));
        Assert.Null(commands.DeletedItems);
        dialogs.FindAll("button").Single(x => x.TextContent.Trim() == "Hủy").Click();
        Assert.Null(commands.DeletedItems);
        page.FindAll("button").Single(x => x.TextContent.Trim() == "Xóa đã chọn (2)").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Xóa 2 dòng", dialogs.Markup, StringComparison.Ordinal));
        dialogs.FindAll("button").Single(x => x.TextContent.Trim() == "Xóa").Click();
        page.WaitForAssertion(() => Assert.Equal(2, commands.DeletedItems?.Length));
        Assert.All(commands.DeletedItems!, item =>
            Assert.Equal(rows.Single(x => x.Id == item.Id).RowVersion, item.RowVersion));
        page.WaitForAssertion(() => Assert.Contains("Xóa đã chọn (0)", page.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public void SampleDetailChangingPageClearsSelectionAndOnlySelectsCurrentPage()
    {
        var query = new FakeProductionQuery
        {
            SampleRows = Enumerable.Range(0, 26).Select(x => SampleRows()[0] with { Id = Guid.NewGuid(), WorkUnitId = Guid.NewGuid(), WorkUnit = $"M-{x:000}" }).ToArray(),
        };
        Services.AddScoped<IProductionModuleQueryService>(_ => query);
        Services.AddScoped<ISampleProductionPlanScenario>(_ => new NoOpSamplePlanService()).AddScenarioRequests();
        var page = Render<Oee.Web.Components.Pages.Production.SamplePlanDetail>(p => p.Add(x => x.Id, Guid.NewGuid()));
        page.WaitForAssertion(() => Assert.Equal(25, page.FindComponents<MudCheckBox<bool>>().Count(box => box.Markup.Contains("Chọn dòng ", StringComparison.Ordinal))));
        page.FindComponents<MudCheckBox<bool>>().Single(box => box.Markup.Contains("Chọn tất cả dòng đang hiển thị", StringComparison.Ordinal)).Find("input").Change(true);
        Assert.Contains("Xóa đã chọn (25)", page.Markup, StringComparison.Ordinal);
        page.Find("button[aria-label='Next page']").Click();
        page.WaitForAssertion(() => Assert.Equal(2, query.LastSamplePage));
        page.WaitForAssertion(() => Assert.Contains("Xóa đã chọn (0)", page.Markup, StringComparison.Ordinal));
        Assert.Single(page.FindComponents<MudCheckBox<bool>>(), box => box.Markup.Contains("Chọn dòng ", StringComparison.Ordinal));
    }

    [Fact]
    public void SampleRowEditUsesMudDialogAndDisplayedConcurrencyToken()
    {
        var rows = SampleRows();
        var query = new FakeProductionQuery { SampleRows = rows };
        var commands = new NoOpSamplePlanService();
        commands.OnItemUpdated = request => query.SampleRows = rows.Select(x =>
            x.Id == rows[0].Id ? x with { PlannedQuantity = request.PlannedQuantity } : x).ToArray();
        Services.AddScoped<IProductionModuleQueryService>(_ => query);
        Services.AddScoped<ISampleProductionPlanScenario>(_ => commands).AddScenarioRequests();
        _ = Menus;
        var dialogs = Render<MudDialogProvider>();
        var page = Render<Oee.Web.Components.Pages.Production.SamplePlanDetail>(p => p.Add(x => x.Id, Guid.NewGuid()));
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll(".row-action-menu").Count));
        SelectAction(page, "Sửa dòng");
        dialogs.WaitForAssertion(() => Assert.Contains("Cập nhật dòng mẫu", dialogs.Markup, StringComparison.Ordinal));
        var editor = dialogs.FindComponent<SamplePlanItemEditDialog>();
        Assert.Equal(3, editor.FindComponents<MudSelect<Guid?>>().Count);
        editor.FindComponent<MudNumericField<decimal>>().Find("input").Change("1500");
        dialogs.FindAll("button").Single(x => x.TextContent.Trim() == "Lưu").Click();
        page.WaitForAssertion(() => Assert.NotNull(commands.UpdatedItem));
        Assert.Equal(rows[0].Id, commands.UpdatedItemId);
        Assert.Equal(Convert.FromBase64String(rows[0].RowVersion), commands.UpdatedToken);
        Assert.Equal(1500m, commands.UpdatedItem!.PlannedQuantity);
        page.WaitForAssertion(() => Assert.Contains(1500m.ToString("N0", System.Globalization.CultureInfo.CurrentCulture), page.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public void SampleCopyFormHasUniquePrefilledCodeAndUsesCopyCommand()
    {
        Services.AddScoped<IProductionModuleQueryService>(_ => new FakeProductionQuery());
        var commands = new NoOpSamplePlanService();
        Services.AddScoped<ISampleProductionPlanScenario>(_ => commands).AddScenarioRequests();
        var sourceId = Guid.NewGuid();
        var navigation = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        navigation.NavigateTo($"/production/plan-templates/new?copyFrom={sourceId}");
        var dialogs = Render<MudDialogProvider>();
        var parameters = new DialogParameters<SamplePlanEditorDialog> { { x => x.CopyFrom, sourceId } };
        _ = Services.GetRequiredService<IDialogService>().ShowAsync<SamplePlanEditorDialog>("Sao chép mẫu", parameters);
        dialogs.WaitForAssertion(() => Assert.Contains("SAMPLE-001-COPY-", dialogs.Markup, StringComparison.Ordinal));
        dialogs.FindAll("button").Single(x => x.TextContent.Trim() == "Lưu mẫu").Click();
        dialogs.WaitForAssertion(() => Assert.Equal(sourceId, commands.CopiedFrom));
        Assert.Contains("-COPY-", commands.CopiedCode, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DowntimeAddOpensPopupAndKeepsOpenEventEndNull()
    {
        var commands = new NoOpDowntimeService();
        Services.AddScoped<IProductionModuleQueryService>(_ => new FakeProductionQuery());
        Services.AddScoped<IDowntimeScenario>(_ => commands).AddScenarioRequests();
        _ = Menus;
        var dialogs = Render<MudDialogProvider>();
        var page = Render<DowntimeEvents>();
        page.FindAll("button").Single(x => x.TextContent.Trim() == "Ghi nhận dừng máy").Click();
        dialogs.WaitForAssertion(() => Assert.Single(dialogs.FindComponents<DowntimeEditorDialog>()));
        var unit = dialogs.FindComponents<MudSelect<string>>().Single(x => x.Instance.Label == "Thiết bị");
        await dialogs.InvokeAsync(() => unit.Instance.ValueChanged.InvokeAsync(WorkUnitId.ToString()));
        var reason = dialogs.FindComponents<MudSelect<string>>().Single(x => x.Instance.Label == "Lý do");
        await dialogs.InvokeAsync(() => reason.Instance.ValueChanged.InvokeAsync(ReasonId.ToString()));
        dialogs.FindAll("button").Single(x => x.TextContent.Trim() == "Lưu downtime").Click();
        dialogs.WaitForAssertion(() => Assert.NotNull(commands.LastRequest));
        Assert.Null(commands.LastRequest!.EndAt);
        Assert.Equal(WorkUnitId, commands.LastRequest.WorkUnitId);
        Assert.Equal(TimeSpan.Zero, commands.LastRequest.StartAt.Offset);
        dialogs.WaitForAssertion(() => Assert.Empty(dialogs.FindComponents<DowntimeEditorDialog>()));
    }

    [Fact]
    public void LegacySampleCopyUrlOpensPopupAndCancelReturnsToList()
    {
        Services.AddScoped<IProductionModuleQueryService>(_ => new FakeProductionQuery());
        Services.AddScoped<ISampleProductionPlanScenario>(_ => new NoOpSamplePlanService()).AddScenarioRequests();
        var dialogs = Render<MudDialogProvider>();
        var navigation = Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
        navigation.NavigateTo($"/production/plan-templates/new?copyFrom={Guid.NewGuid()}");
        var page = Render<SamplePlanEditor>();
        dialogs.WaitForAssertion(() => Assert.Contains("SAMPLE-001-COPY-", dialogs.Markup, StringComparison.Ordinal));
        Assert.Empty(page.FindAll("form"));
        dialogs.FindAll("button").Single(x => x.TextContent.Trim() == "Hủy").Click();
        dialogs.WaitForAssertion(() => Assert.EndsWith("/production/plan-templates", navigation.Uri, StringComparison.Ordinal));
    }

    [Fact]
    public async Task BulkDowntimePopupSendsEachSelectedConcurrencyToken()
    {
        var commands = new NoOpDowntimeService();
        Services.AddScoped<IDowntimeScenario>(_ => commands).AddScenarioRequests();
        _ = Menus;
        var dialogs = Render<MudDialogProvider>();
        var rows = (await new FakeProductionQuery().DowntimeAsync(new DowntimeListQuery(), CancellationToken.None)).Items.ToArray();
        var items = new[] { rows[0], rows[0] with { Id = Guid.NewGuid(), ConcurrencyToken = Convert.ToBase64String([2]) } };
        var reasonId = Guid.NewGuid();
        var parameters = new DialogParameters<DowntimeReclassifyDialog>
        {
            { x => x.Items, items },
            { x => x.Reasons, new ProductionLookupOption[] { new(reasonId, "DR-FAILURE", "Hỏng máy") } }
        };
        await Services.GetRequiredService<IDialogService>().ShowAsync<DowntimeReclassifyDialog>("Tái phân loại", parameters);
        dialogs.WaitForAssertion(() => Assert.Single(dialogs.FindComponents<MudSelect<Guid?>>()));
        var select = dialogs.FindComponent<MudSelect<Guid?>>();
        await dialogs.InvokeAsync(() => select.Instance.ValueChanged.InvokeAsync(reasonId));
        dialogs.FindAll("button").Single(x => x.TextContent.Trim() == "Tái phân loại").Click();
        dialogs.WaitForAssertion(() => Assert.NotNull(commands.LastBulkItems));
        var savedItems = commands.LastBulkItems!;
        Assert.Equal(items.Select(x => x.Id), savedItems.Select(x => x.DowntimeId));
        Assert.Equal(items.Select(x => x.ConcurrencyToken), savedItems.Select(x => x.ConcurrencyToken));
        Assert.Equal(reasonId, commands.LastBulkReason);
        dialogs.WaitForAssertion(() => Assert.Empty(dialogs.FindComponents<DowntimeReclassifyDialog>()));
    }

    private static SamplePlanItemDetail[] SampleRows() =>
    [
        new(Guid.NewGuid(), WorkUnitId, "M-001", ProductId, "SKU-001", ShiftId, "S1", 1000m, null,
            Convert.ToBase64String([1]), "Blow Moulder", "Sản phẩm", "Ca đêm", new(22, 0), new(6, 0)),
        new(Guid.NewGuid(), WorkUnitId, "M-001", ProductId, "SKU-001", Guid.NewGuid(), "S2", 1000m, null,
            Convert.ToBase64String([2]), "Blow Moulder", "Sản phẩm", "Ca sáng", new(6, 0), new(14, 0)),
    ];

    private sealed class FakeProductionQuery(bool failPlans = false) : IProductionModuleQueryService
    {
        public SamplePlanItemDetail[] SampleRows { get; set; } = [];
        public int LastSamplePage { get; private set; }
        public bool PlanDeleted { get; set; }
        public string LookupProductCode { get; set; } = "SKU-001";
        public ProductionPlanExecutionStatusEnum ExecutionStatus { get; set; } = ProductionPlanExecutionStatusEnum.InProgress;
        public decimal ActualQuantity { get; set; } = 600m;
        public ProductionPlanListQuery? LastPlanQuery { get; private set; }

        public Task<ProductionLookups> LookupsAsync(CancellationToken cancellationToken) => Task.FromResult(new ProductionLookups(
            [new(WorkUnitId, "M-001", "Blow Moulder", Guid.NewGuid(), "SE Asia Standard Time")],
            [], [new(ProductId, LookupProductCode, "Sản phẩm")], [new(ShiftId, "S1", "Ca 1", StartTime: new(22, 0), EndTime: new(6, 0))],
            [new(ReasonId, "DR-UNKNOWN", "Chưa phân loại")]));

        public Task<PagedResult<ProductionPlanListItem>> PlansAsync(ProductionPlanListQuery query, CancellationToken cancellationToken)
        {
            LastPlanQuery = query;
            if (failPlans) return Task.FromException<PagedResult<ProductionPlanListItem>>(new InvalidOperationException("database unavailable"));
            if (PlanDeleted) return Task.FromResult(new PagedResult<ProductionPlanListItem>([], 0));
            ProductionPlanListItem item = new(Guid.NewGuid(), "PLAN-001", WorkUnitId, "M-001", ProductId, "SKU-001",
                ShiftId, "S1", Now.AddHours(-1), Now.AddHours(7), 1_200m, ActualQuantity, ActualQuantity / 1200m, 1,
                Convert.ToBase64String([1]), true, ExecutionStatus)
            {
                WorkUnitName = "Blow Moulder",
                ProductName = "Wakeup 247"
            };
            return Task.FromResult(new PagedResult<ProductionPlanListItem>([item], 1));
        }

        public Task<PagedResult<DowntimeListItem>> DowntimeAsync(DowntimeListQuery query, CancellationToken cancellationToken)
        {
            DowntimeListItem item = new(Guid.NewGuid(), WorkUnitId, "M-001", ReasonId, "DR-UNKNOWN", "Chưa phân loại",
                Now.AddMinutes(-10), null, null, "Unplanned", "Failure", "Automatic", "PLC fault", null, "initial");
            var items = query.PageSize > 1 ? new[] { item } : [];
            return Task.FromResult(new PagedResult<DowntimeListItem>(items, 1));
        }

        public Task<DowntimeListItem> DowntimeAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromException<DowntimeListItem>(new KeyNotFoundException());
        public Task<PagedResult<SamplePlanListItem>> SamplesAsync(SamplePlanListQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResult<SamplePlanListItem>(
                [new(Guid.Parse("70000000-0000-0000-0000-000000000001"), "SAMPLE-001", "Mẫu", null, 1, 1, 1, 1000, Now, Convert.ToBase64String([1]))], 1));
        public Task<Oee.Application.Contracts.Modules.SamplePlanDetail> SampleAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(new Oee.Application.Contracts.Modules.SamplePlanDetail(id, "SAMPLE-001", "Mẫu", null, SampleRows.Length,
                SampleRows.Select(x => x.WorkUnitId).Distinct().Count(), SampleRows.Select(x => x.ShiftId).Distinct().Count(),
                SampleRows.Sum(x => x.PlannedQuantity), Convert.ToBase64String([1])));
        public Task<PagedResult<SamplePlanItemDetail>> SampleItemsAsync(Guid samplePlanId, string? sortBy, bool descending, int page, int pageSize, CancellationToken cancellationToken) =>
            SamplePage(page, pageSize);
        private Task<PagedResult<SamplePlanItemDetail>> SamplePage(int page, int pageSize)
        {
            LastSamplePage = page;
            return Task.FromResult(new PagedResult<SamplePlanItemDetail>(SampleRows.Skip((page - 1) * pageSize).Take(pageSize).ToArray(), SampleRows.Length));
        }
    }

    private sealed class NoOpProductionPlanService : IProductionPlanScenario
    {
        public int DeleteCalls { get; private set; }
        public ProductionPlanRequest? SavedRequest { get; private set; }
        public bool HasOutput { get; set; }
        public int UpdateCalls { get; private set; }
        public int CorrectionCalls { get; private set; }
        public Action? OnDeleted { get; set; }
        private ProductionPlanResponse Response => new(Guid.Empty, Guid.Empty, 1,
            WorkUnitId, ProductId, ShiftId, "PLAN-001", Now, Now.AddHours(1), 1m, null, Convert.ToBase64String([1]), HasOutput);
        public Task<ProductionPlanResponse> CreateAsync(ProductionPlanRequest request, CancellationToken cancellationToken) => Task.FromResult(Response);
        public Task<ProductionPlanResponse> UpdateAsync(Guid id, ProductionPlanRequest request, byte[] expectedRowVersion, CancellationToken cancellationToken) { UpdateCalls++; SavedRequest = request; return Task.FromResult(Response); }
        public Task<ProductionPlanResponse> CorrectAsync(Guid id, ProductionPlanRequest request, byte[] expectedRowVersion, CancellationToken cancellationToken) { CorrectionCalls++; SavedRequest = request; return Task.FromResult(Response); }
        public Task DeleteAsync(Guid id, byte[] expectedRowVersion, CancellationToken cancellationToken)
        {
            Assert.Equal(new byte[] { 1 }, expectedRowVersion);
            DeleteCalls++;
            OnDeleted?.Invoke();
            return Task.CompletedTask;
        }
        public Task<ProductionPlanResponse> GetAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Response);
        public Task<PagedProductionPlanResponse> ListAsync(DateTimeOffset? from, DateTimeOffset? until, int page, int pageSize, CancellationToken cancellationToken) => Task.FromResult(new PagedProductionPlanResponse([], 0));
        public Task<int> GenerateFromSampleAsync(GenerateSamplePlanRequest request, CancellationToken cancellationToken) => Task.FromResult(0);
        public Task<BulkProductionPlanPreview> PreviewBulkAsync(BulkProductionPlanRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new BulkProductionPlanPreview(0, 0, 0, 0, []));
        public Task<BulkProductionPlanResult> CreateBulkAsync(BulkProductionPlanRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new BulkProductionPlanResult(0, 0, 0, 0, [], []));
    }

    private sealed class NoOpSamplePlanService : ISampleProductionPlanScenario
    {
        public SamplePlanItemConcurrency[]? DeletedItems { get; private set; }
        public Action? OnItemDeleted { get; set; }
        public SamplePlanItemRequest? UpdatedItem { get; private set; }
        public Guid? UpdatedItemId { get; private set; }
        public byte[]? UpdatedToken { get; private set; }
        public Action<SamplePlanItemRequest>? OnItemUpdated { get; set; }
        public Guid? CopiedFrom { get; private set; }
        public string? CopiedCode { get; private set; }
        private static SamplePlanResponse Parent => new(Guid.NewGuid(), "SAMPLE", "Sample", null, Convert.ToBase64String([1]));
        public Task<SamplePlanResponse> CreateAsync(SamplePlanRequest request, CancellationToken cancellationToken) => Task.FromResult(Parent);
        public Task<SamplePlanResponse> UpdateAsync(Guid id, SamplePlanRequest request, byte[] expectedRowVersion, CancellationToken cancellationToken) => Task.FromResult(Parent);
        public Task<SamplePlanResponse> CopyAsync(Guid id, string code, string name, CancellationToken cancellationToken)
        { CopiedFrom = id; CopiedCode = code; return Task.FromResult(Parent); }
        public Task DeleteAsync(Guid id, byte[] expectedRowVersion, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<SamplePlanItemResponse> UpdateItemAsync(Guid itemId, SamplePlanItemRequest request, byte[] expectedRowVersion, CancellationToken cancellationToken)
        { UpdatedItem = request; UpdatedItemId = itemId; UpdatedToken = expectedRowVersion; OnItemUpdated?.Invoke(request); return Task.FromResult(new SamplePlanItemResponse(itemId, Guid.NewGuid(), request.WorkUnitId, request.ProductId, request.ShiftId, request.PlannedQuantity, request.Notes, Convert.ToBase64String([1]))); }
        public Task DeleteItemsAsync(IReadOnlyCollection<SamplePlanItemConcurrency> items, CancellationToken cancellationToken)
        { DeletedItems = items.ToArray(); OnItemDeleted?.Invoke(); return Task.CompletedTask; }
        public Task<SamplePlanBulkResult> CreateWithItemsAsync(SamplePlanBulkCreateRequest request, CancellationToken cancellationToken) => Task.FromResult(new SamplePlanBulkResult(Guid.NewGuid(), 1, 1, 0, [], []));
        public Task<SamplePlanBulkPreview> PreviewAddItemsAsync(Guid samplePlanId, SamplePlanBulkAddItemsRequest request, CancellationToken cancellationToken) => Task.FromResult(new SamplePlanBulkPreview(1, 1, 0, []));
        public Task<SamplePlanBulkResult> AddItemsAsync(Guid samplePlanId, SamplePlanBulkAddItemsRequest request, CancellationToken cancellationToken) => Task.FromResult(new SamplePlanBulkResult(samplePlanId, 1, 1, 0, [], []));
    }

    private sealed class NoOpDowntimeService : IDowntimeScenario
    {
        public DowntimeRequest? LastRequest { get; private set; }
        public IReadOnlyCollection<BulkReclassifyDowntimeItem>? LastBulkItems { get; private set; }
        public Guid? LastBulkReason { get; private set; }
        public Task<Guid> CreateAsync(DowntimeRequest request, CancellationToken cancellationToken) { LastRequest = request; return Task.FromResult(Guid.NewGuid()); }
        public Task CloseAsync(Guid id, DateTimeOffset endAt, string concurrencyToken, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ReclassifyAsync(Guid id, Guid reasonId, string concurrencyToken, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<int> BulkReclassifyAsync(IReadOnlyCollection<BulkReclassifyDowntimeItem> items, Guid reasonId, CancellationToken cancellationToken) { LastBulkItems = items; LastBulkReason = reasonId; return Task.FromResult(items.Count); }
    }
}
