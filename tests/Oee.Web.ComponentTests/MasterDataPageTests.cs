using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Extensions;
using MudBlazor.Services;
using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.Operations;
using Oee.Application.Modules;
using Oee.Application.Security;
using Oee.Web.Components.Pages.Master;
using Oee.Application.Catalog.Products.Create;
using Oee.Application.Catalog.Products.SetProductStatus;
using Oee.Application.Catalog.Products.Update;
using Oee.Application.Catalog.Standards.Create;
using Oee.Application.Catalog.Standards.EndStandardEffectiveness;
using Oee.Application.Downtime.Reasons.Create;
using Oee.Application.Downtime.Reasons.SetDowntimeReasonStatus;
using Oee.Application.Downtime.Reasons.Update;
using Oee.Application.Organizations.Areas.Create;
using Oee.Application.Organizations.Areas.SetAreaStatus;
using Oee.Application.Organizations.Areas.Update;
using Oee.Application.Organizations.Enterprises.Create;
using Oee.Application.Organizations.Enterprises.SetEnterpriseStatus;
using Oee.Application.Organizations.Enterprises.Update;
using Oee.Application.Organizations.Shifts.Create;
using Oee.Application.Organizations.Shifts.SetShiftStatus;
using Oee.Application.Organizations.Shifts.Update;
using Oee.Application.Organizations.Sites.Create;
using Oee.Application.Organizations.Sites.SetSiteStatus;
using Oee.Application.Organizations.Sites.Update;
using Oee.Application.Organizations.WorkCenters.Create;
using Oee.Application.Organizations.WorkCenters.SetWorkCenterStatus;
using Oee.Application.Organizations.WorkCenters.Update;
using Oee.Application.Organizations.WorkUnits.Create;
using Oee.Application.Organizations.WorkUnits.SetWorkUnitStatus;
using Oee.Application.Organizations.WorkUnits.Update;

namespace Oee.Web.ComponentTests;

/// <summary>
/// bUnit Test cho các page CRUD master, dialog tạo/sửa, status action và quyền MasterDataWrite.
/// </summary>
public sealed class MasterDataPageTests : RowMenuTestContext, IAsyncLifetime
{
    private static readonly Guid EnterpriseId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid SiteId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid AreaId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid GroupId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    private static readonly Guid WorkUnitId = Guid.Parse("50000000-0000-0000-0000-000000000001");
    private static readonly Guid ProductId = Guid.Parse("60000000-0000-0000-0000-000000000001");
    private static readonly Guid CopyProductId = Guid.Parse("60000000-0000-0000-0000-000000000002");
    private static readonly DateTimeOffset Updated = new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);
    private readonly BunitAuthorizationContext authorization;

    /// <summary>Chuẩn bị principal PlanningManager, MudBlazor và command service giả cho UI test.</summary>
    public MasterDataPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddAuthorizationCore(options =>
            options.AddPolicy(Capabilities.MasterDataWrite, policy => policy.RequireRole("Admin", "PlanningManager")));
        authorization = this.AddAuthorization();
        authorization.SetAuthorized("planner");
        authorization.SetRoles("PlanningManager");
        authorization.SetPolicies(Capabilities.MasterDataWrite);
        Services.AddScoped<IMasterDataScenario>(_ => new NoOpMasterCommands()).AddScenarioRequests();
    }

    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;
    async Task IAsyncLifetime.DisposeAsync() => await base.DisposeAsync();

    [Fact]
    public void ProductsRenderServerResultAndPlanningActions()
    {
        // Arrange: fake query trả một SKU và lưu lại request paging.
        var query = new FakeMasterQuery();
        Services.AddScoped<IMasterDataQueryService>(_ => query);

        // Act & Assert: PlanningManager thấy row/action và page dùng server paging 1/25.
        var component = Render<Products>();

        component.WaitForAssertion(() =>
        {
            Assert.Contains("SKU-001", component.Markup, StringComparison.Ordinal);
            Assert.Contains("Thêm sản phẩm", component.Markup, StringComparison.Ordinal);
            Assert.Single(component.FindAll(".row-action-menu"));
            Assert.NotNull(query.LastProductQuery);
            Assert.Equal(1, query.LastProductQuery.Page);
            Assert.Equal(25, query.LastProductQuery.PageSize);
        });
        OpenActions(component);
        Assert.NotNull(MenuAction("Chỉnh sửa sản phẩm"));
    }

    [Fact]
    public void ViewerCannotSeeMasterMutationActions()
    {
        // Arrange: đổi principal sang Viewer không có MasterDataWrite.
        authorization.SetRoles("Viewer");
        authorization.SetPolicies();
        Services.AddScoped<IMasterDataQueryService>(_ => new FakeMasterQuery());

        // Act & Assert: dữ liệu vẫn đọc được nhưng create/edit/delete bị ẩn tại UI.
        var component = Render<Products>();

        component.WaitForAssertion(() => Assert.Contains("SKU-001", component.Markup, StringComparison.Ordinal));
        Assert.DoesNotContain("Thêm sản phẩm", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Sửa sản phẩm", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Xóa sản phẩm", component.Markup, StringComparison.Ordinal);
        Assert.Empty(component.FindAll(".row-action-menu"));
    }

    [Fact]
    public void ProductQueryFailureShowsFriendlyAlertAndKeepsRefresh()
    {
        // Arrange & Act: query sản phẩm ném lỗi database.
        Services.AddScoped<IMasterDataQueryService>(_ => new FakeMasterQuery(failProducts: true));
        var component = Render<Products>();
        // Assert: page không lộ lỗi hạ tầng và vẫn cung cấp Làm mới tại PageHeader.
        component.WaitForAssertion(() =>
        {
            Assert.Contains("Làm mới", component.Markup, StringComparison.Ordinal);
            Assert.Contains("Không thể tải sản phẩm.", component.Find(".mud-alert").TextContent, StringComparison.Ordinal);
            Assert.DoesNotContain("database unavailable", component.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void ShiftPageLabelsCrossMidnightShift()
    {
        // Arrange & Act: DTO ca S3 có StartTime 22:00, EndTime 06:00 và CrossesMidnight=true.
        Services.AddScoped<IMasterDataQueryService>(_ => new FakeMasterQuery());
        var component = Render<Shifts>();
        // Assert: UI giải thích rõ ca qua nửa đêm và giữ đúng TimeOnly.
        component.WaitForAssertion(() =>
        {
            Assert.Contains("S3", component.Markup, StringComparison.Ordinal);
            Assert.Contains("Qua nửa đêm", component.Markup, StringComparison.Ordinal);
            Assert.Contains("22:00", component.Markup, StringComparison.Ordinal);
            Assert.Contains("06:00", component.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task SiteEditorLoadsTimeZoneAllowsCodeAndKeepsParentReadOnly()
    {
        // Editor này được cây tổ chức sử dụng; runtime được kiểm tra ở EquipmentHierarchyPageTests.
        Services.AddScoped<IMasterDataQueryService>(_ => new FakeMasterQuery());
        _ = Menus;
        var dialogs = Render<MudDialogProvider>();
        var parameters = new DialogParameters<MasterDataEditDialog>
        {
            { dialog => dialog.Kind, MasterEditKind.Site },
            { dialog => dialog.Id, SiteId },
        };
        await Services.GetRequiredService<IDialogService>().ShowAsync<MasterDataEditDialog>("Cập nhật nhà máy", parameters);
        dialogs.WaitForAssertion(() =>
        {
            Assert.Contains("SE Asia Standard Time", dialogs.Markup, StringComparison.Ordinal);
            var code = dialogs.FindAll("input").Single(input => input.GetAttribute("value") == "SITE-01");
            Assert.False(code.HasAttribute("disabled"));
            var parent = dialogs.FindAll("input").Single(input => input.GetAttribute("value") == "ENT-01 — Enterprise");
            Assert.True(parent.HasAttribute("disabled"));
        });
    }

    [Theory]
    [InlineData(MasterEditKind.Enterprise, "Root")]
    [InlineData(MasterEditKind.Site, "Stored description")]
    [InlineData(MasterEditKind.Area, "Stored description")]
    [InlineData(MasterEditKind.WorkCenter, "Stored description")]
    [InlineData(MasterEditKind.WorkUnit, "Stored description")]
    public async Task HierarchyEditorsLoadAndSaveCodeAndDescription(MasterEditKind kind, string stored)
    {
        var commands = new NoOpMasterCommands();
        Services.AddScoped<IMasterDataQueryService>(_ => new FakeMasterQuery());
        Services.AddScoped<IMasterDataScenario>(_ => commands).AddScenarioRequests();
        _ = Menus;
        var dialogs = Render<MudDialogProvider>();
        var parameters = new DialogParameters<MasterDataEditDialog>
        {
            { dialog => dialog.Kind, kind }, { dialog => dialog.Id, SiteId },
        };
        await Services.GetRequiredService<IDialogService>().ShowAsync<MasterDataEditDialog>("Cập nhật", parameters);
        dialogs.WaitForAssertion(() => Assert.Equal(stored, dialogs.Find("textarea").TextContent));
        var code = dialogs.FindComponents<MudTextField<string>>().Single(x => x.Instance.Label?.StartsWith("Mã", StringComparison.Ordinal) == true);
        Assert.False(code.Find("input").HasAttribute("disabled"));
        code.Find("input").Change("NEW-CODE");
        dialogs.Find("textarea").Change("Mô tả mới");
        dialogs.WaitForAssertion(() =>
        {
            var save = dialogs.FindAll("button").Single(x => x.TextContent.Trim() == "Lưu");
            Assert.False(save.HasAttribute("disabled"));
            save.Click();
        });
        dialogs.WaitForAssertion(() => Assert.Equal("Mô tả mới", commands.LastHierarchyDescription));
        Assert.Equal("NEW-CODE", commands.LastHierarchyCode);
    }

    [Fact]
    public async Task EnterpriseCreatorSavesDescription()
    {
        var commands = new NoOpMasterCommands();
        Services.AddScoped<IMasterDataQueryService>(_ => new FakeMasterQuery());
        Services.AddScoped<IMasterDataScenario>(_ => commands).AddScenarioRequests();
        _ = Menus;
        var dialogs = Render<MudDialogProvider>();
        var parameters = new DialogParameters<MasterDataCreateDialog> { { dialog => dialog.Kind, MasterCreateKind.Enterprise } };
        await Services.GetRequiredService<IDialogService>().ShowAsync<MasterDataCreateDialog>("Thêm doanh nghiệp", parameters);
        dialogs.WaitForAssertion(() => Assert.Single(dialogs.FindAll("textarea")));
        var fields = dialogs.FindAll("input");
        fields[0].Change("ENT-DESC");
        fields[1].Change("Enterprise");
        dialogs.Find("textarea").Change("Mô tả doanh nghiệp");
        dialogs.WaitForAssertion(() =>
        {
            var save = dialogs.FindAll("button").Single(x => x.TextContent.Trim() == "Lưu");
            Assert.False(save.HasAttribute("disabled"));
            save.Click();
        });
        dialogs.WaitForAssertion(() => Assert.Equal("Mô tả doanh nghiệp", commands.LastHierarchyDescription));
    }

    [Fact]
    public void ProductCreatePopupSavesAndReloadsParentOnlyAfterDialogResultOk()
    {
        // Arrange: render page cùng MudDialogProvider và ghi lại command create.
        var query = new FakeMasterQuery();
        var commands = new NoOpMasterCommands();
        Services.AddScoped<IMasterDataQueryService>(_ => query);
        Services.AddScoped<IMasterDataScenario>(_ => commands).AddScenarioRequests();
        _ = Menus;
        var dialogs = Render<MudDialogProvider>();
        var page = Render<Products>();
        page.WaitForAssertion(() => Assert.True(query.ProductQueryCount >= 1));

        // Act: mở dialog từ nút thật, nhập form và bấm Lưu khi validation cho phép.
        page.FindAll("button").Single(button => button.TextContent.Contains("Thêm sản phẩm", StringComparison.Ordinal)).Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Thêm sản phẩm mới", dialogs.Markup, StringComparison.Ordinal));
        var inputs = dialogs.FindAll("input");
        inputs[0].Change("  SKU-POPUP  ");
        inputs[1].Change("Popup Product");
        inputs[2].Change("PCS");
        dialogs.WaitForAssertion(() =>
        {
            var save = dialogs.FindAll("button").Single(button => button.TextContent.Trim() == "Lưu");
            Assert.False(save.HasAttribute("disabled"));
            save.Click();
        });

        // Assert: dialog chỉ đóng sau command thành công và bảng cha query lại.
        dialogs.WaitForAssertion(() => Assert.DoesNotContain("Thêm sản phẩm mới", dialogs.Markup, StringComparison.Ordinal));
        page.WaitForAssertion(() => Assert.True(query.ProductQueryCount >= 2));
        Assert.NotNull(commands.LastCreatedProduct);
        Assert.Equal("  SKU-POPUP  ", commands.LastCreatedProduct.Code);
    }

    [Fact]
    public void ProductCreatePopupStaysOpenWhenServerSaveFails()
    {
        // Arrange: command create được cấu hình trả lỗi SQL.
        Services.AddScoped<IMasterDataQueryService>(_ => new FakeMasterQuery());
        Services.AddScoped<IMasterDataScenario>(_ => new NoOpMasterCommands(failProductCreate: true)).AddScenarioRequests();
        _ = Menus;
        var dialogs = Render<MudDialogProvider>();
        var page = Render<Products>();
        page.WaitForAssertion(() => Assert.Contains("SKU-001", page.Markup, StringComparison.Ordinal));

        // Act: nhập form hợp lệ và bấm Lưu.
        page.FindAll("button").Single(button => button.TextContent.Contains("Thêm sản phẩm", StringComparison.Ordinal)).Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Thêm sản phẩm mới", dialogs.Markup, StringComparison.Ordinal));
        var inputs = dialogs.FindAll("input");
        inputs[0].Change("SKU-FAIL"); inputs[1].Change("Failure"); inputs[2].Change("PCS");
        // Assert: dialog không đóng và hiển thị lỗi server cho người dùng sửa/thử lại.
        dialogs.WaitForAssertion(() =>
        {
            var save = dialogs.FindAll("button").Single(button => button.TextContent.Trim() == "Lưu");
            Assert.False(save.HasAttribute("disabled"));
            save.Click();
        });
        dialogs.WaitForAssertion(() =>
        {
            Assert.Contains("Thêm sản phẩm mới", dialogs.Markup, StringComparison.Ordinal);
            Assert.Contains("sql unavailable", dialogs.Markup, StringComparison.Ordinal);
        });
    }

    [Theory]
    [InlineData(MasterCreateKind.Product, "Thêm sản phẩm mới")]
    [InlineData(MasterCreateKind.WorkUnitStandard, "Thêm định mức tốc độ")]
    [InlineData(MasterCreateKind.Shift, "Thêm ca làm việc")]
    [InlineData(MasterCreateKind.DowntimeReason, "Thêm lý do dừng máy")]
    [InlineData(MasterCreateKind.WorkUnit, "Thêm thiết bị")]
    [InlineData(MasterCreateKind.WorkCenter, "Thêm dây chuyền")]
    [InlineData(MasterCreateKind.Area, "Thêm khu vực sản xuất")]
    [InlineData(MasterCreateKind.Enterprise, "Thêm doanh nghiệp")]
    [InlineData(MasterCreateKind.Site, "Thêm nhà máy")]
    public async Task EachMasterCreatePopupUsesMudDialogAndLoadsActiveLookups(MasterCreateKind kind, string title)
    {
        // Arrange: mỗi theory case đại diện một trong chín loại master create.
        Services.AddScoped<IMasterDataQueryService>(_ => new FakeMasterQuery());
        _ = Menus;
        var provider = Render<MudDialogProvider>();
        var parameters = new DialogParameters<MasterDataCreateDialog> { { dialog => dialog.Kind, kind } };

        // Act: mở component dùng chung qua IDialogService giống trang thật.
        await Services.GetRequiredService<IDialogService>().ShowAsync<MasterDataCreateDialog>(title, parameters);

        // Assert: dialog là MudBlazor thật và luôn có action Hủy/Lưu.
        provider.WaitForAssertion(() =>
        {
            Assert.Contains(title, provider.Markup, StringComparison.Ordinal);
            Assert.Contains("mud-dialog", provider.Markup, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Hủy", provider.Markup, StringComparison.Ordinal);
            Assert.Contains("Lưu", provider.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task DowntimeReasonPopupDefaultsToAValidDomainKind()
    {
        // Arrange & Act: mở popup DowntimeReason với lookup taxonomy Domain.
        Services.AddScoped<IMasterDataQueryService>(_ => new FakeMasterQuery());
        _ = Menus;
        var provider = Render<MudDialogProvider>();
        var parameters = new DialogParameters<MasterDataCreateDialog>
        {
            { dialog => dialog.Kind, MasterCreateKind.DowntimeReason },
        };

        await Services.GetRequiredService<IDialogService>()
            .ShowAsync<MasterDataCreateDialog>("Thêm lý do dừng máy", parameters);

        // Assert: Kind mặc định hợp lệ với Category, không đưa SetupChangeover sai tổ hợp.
        provider.WaitForAssertion(() =>
        {
            Assert.Contains("Breakdown", provider.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("SetupChangeover", provider.Markup, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void ProductEditDialogReloadsByIdKeepsCodeReadOnlyAndReloadsParentAfterSuccess()
    {
        // Arrange: page có query-by-id, command update và dialog provider.
        var query = new FakeMasterQuery();
        var commands = new NoOpMasterCommands();
        Services.AddScoped<IMasterDataQueryService>(_ => query);
        Services.AddScoped<IMasterDataScenario>(_ => commands).AddScenarioRequests();
        _ = Menus;
        var dialogs = Render<MudDialogProvider>();
        var page = Render<Products>();
        page.WaitForAssertion(() => Assert.Contains("SKU-001", page.Markup, StringComparison.Ordinal));
        var countBefore = query.ProductQueryCount;

        // Act: mở edit từ icon thật, xác nhận code disabled, đổi tên rồi lưu.
        SelectAction(page, "Chỉnh sửa sản phẩm");
        dialogs.WaitForAssertion(() => Assert.Contains("Chỉnh sửa sản phẩm", dialogs.Markup, StringComparison.Ordinal));
        Assert.True(dialogs.FindAll("input").First(input => input.GetAttribute("value") == "SKU-001").HasAttribute("disabled"));
        var name = dialogs.FindAll("input").First(input => input.GetAttribute("value") == "Product");
        name.Change("Product updated");
        dialogs.FindAll("button").Single(button => button.TextContent.Trim() == "Lưu").Click();

        // Assert: command nhận đúng Id và bảng cha reload chỉ sau DialogResult.Ok.
        dialogs.WaitForAssertion(() => Assert.DoesNotContain("Chỉnh sửa sản phẩm", dialogs.Markup, StringComparison.Ordinal));
        page.WaitForAssertion(() => Assert.True(query.ProductQueryCount > countBefore));
        Assert.NotNull(commands.LastProductUpdate);
        Assert.Equal(ProductId, commands.LastProductUpdate.Id);
        Assert.Equal("Product updated", commands.LastProductUpdate.Name);
    }

    [Fact]
    public void StatusActionUsesDynamicTooltipConfirmationAndReloadsOnlyAfterSuccess()
    {
        // Arrange: SKU Active có icon/tooltip Tạm ngưng.
        var query = new FakeMasterQuery();
        var commands = new NoOpMasterCommands();
        Services.AddScoped<IMasterDataQueryService>(_ => query);
        Services.AddScoped<IMasterDataScenario>(_ => commands).AddScenarioRequests();
        _ = Menus;
        var dialogs = Render<MudDialogProvider>();
        var page = Render<Products>();
        page.WaitForAssertion(() => Assert.Contains("SKU-001", page.Markup, StringComparison.Ordinal));
        var countBefore = query.ProductQueryCount;

        // Act: mở confirm dialog và thực hiện mutation trạng thái.
        SelectAction(page, "Tạm ngưng hoạt động");
        dialogs.WaitForAssertion(() =>
        {
            Assert.Contains("Trạng thái không được tự động cascade", dialogs.Markup, StringComparison.Ordinal);
            Assert.Contains("SKU-001", dialogs.Markup, StringComparison.Ordinal);
        });
        dialogs.FindAll("button").Single(button => button.TextContent.Trim() == "Tạm ngưng").Click();

        // Assert: request target Inactive gửi đúng một lần và page reload sau thành công.
        dialogs.WaitForAssertion(() => Assert.DoesNotContain("Trạng thái không được tự động cascade", dialogs.Markup, StringComparison.Ordinal));
        page.WaitForAssertion(() => Assert.True(query.ProductQueryCount > countBefore));
        Assert.Equal(1, commands.ProductStatusCallCount);
        Assert.Equal("Inactive", commands.LastStatusRequest?.TargetStatus);
    }

    [Fact]
    public void ProductCopyPreservesSourceFieldsRequiresNewCodeAndCreatesWithoutUpdatingSource()
    {
        var query = new FakeMasterQuery { ProductSourceStatus = "Inactive" };
        var commands = new NoOpMasterCommands();
        Services.AddScoped<IMasterDataQueryService>(_ => query);
        Services.AddScoped<IMasterDataScenario>(_ => commands).AddScenarioRequests();
        _ = Menus;
        var dialogs = Render<MudDialogProvider>();
        var page = Render<Products>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".row-action-menu")));
        var queriesBefore = query.ProductQueryCount;

        SelectAction(page, "Sao chép sản phẩm");
        dialogs.WaitForAssertion(() => Assert.Contains("Sao chép từ SKU-001", dialogs.Markup, StringComparison.Ordinal));
        var fields = dialogs.FindComponents<MudTextField<string>>();
        Assert.Equal("SKU-001", fields.Single(field => field.Instance.Label == "Mã sản phẩm").Instance.GetState(field => field.Value));
        Assert.Equal("Product", fields.Single(field => field.Instance.Label == "Tên sản phẩm").Instance.GetState(field => field.Value));
        Assert.Equal("PCS", fields.Single(field => field.Instance.Label == "Đơn vị mặc định").Instance.GetState(field => field.Value));
        Assert.Equal("Description", fields.Single(field => field.Instance.Label == "Mô tả").Instance.GetState(field => field.Value));
        Assert.Equal("Inactive", dialogs.FindComponent<MudSelect<string>>().Instance.GetState(select => select.Value));
        Assert.Null(commands.LastCreatedProduct);

        dialogs.FindAll("button").Single(button => button.TextContent.Trim() == "Lưu").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Hãy nhập mã SKU mới", dialogs.Markup, StringComparison.Ordinal));
        Assert.Null(commands.LastCreatedProduct);
        fields.Single(field => field.Instance.Label == "Mã sản phẩm").Find("input").Change("SKU-NEW");
        dialogs.FindAll("button").Single(button => button.TextContent.Trim() == "Lưu").Click();

        page.WaitForAssertion(() => Assert.True(query.ProductQueryCount > queriesBefore));
        Assert.Equal(new CreateProductCommand("SKU-NEW", "Product", "PCS", "Description", "Inactive"), commands.LastCreatedProduct);
        Assert.Null(commands.LastProductUpdate);
        Assert.Equal(0, commands.ProductStatusCallCount);
    }

    [Fact]
    public void ProductCopyCancelDoesNotCreateOrReload()
    {
        var query = new FakeMasterQuery();
        var commands = new NoOpMasterCommands();
        Services.AddScoped<IMasterDataQueryService>(_ => query);
        Services.AddScoped<IMasterDataScenario>(_ => commands).AddScenarioRequests();
        _ = Menus;
        var dialogs = Render<MudDialogProvider>();
        var page = Render<Products>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".row-action-menu")));
        var queriesBefore = query.ProductQueryCount;
        SelectAction(page, "Sao chép sản phẩm");
        dialogs.WaitForAssertion(() => Assert.Contains("Sao chép từ SKU-001", dialogs.Markup, StringComparison.Ordinal));

        dialogs.FindAll("button").Single(button => button.TextContent.Trim() == "Hủy").Click();

        dialogs.WaitForAssertion(() => Assert.Empty(dialogs.FindComponents<MasterDataCreateDialog>()));
        Assert.Null(commands.LastCreatedProduct);
        Assert.Null(commands.LastCreatedStandard);
        Assert.Equal(queriesBefore, query.ProductQueryCount);
    }

    [Fact]
    public async Task StandardCopyPreservesSpeedDatesAndApprovalAndCreatesForDestinationSku()
    {
        var query = new FakeMasterQuery
        {
            StandardSource = new(Guid.NewGuid(), WorkUnitId, "M-001", ProductId, "SKU-001", .2m, 12m,
                "Pallet", new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), "Engineer", false, Updated, "AQ==")
        };
        var commands = new NoOpMasterCommands();
        Services.AddScoped<IMasterDataQueryService>(_ => query);
        Services.AddScoped<IMasterDataScenario>(_ => commands).AddScenarioRequests();
        _ = Menus;
        var dialogs = Render<MudDialogProvider>();
        var page = Render<WorkUnitStandards>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".row-action-menu")));

        SelectAction(page, "Sao chép định mức");
        dialogs.WaitForAssertion(() => Assert.Contains("Sao chép từ M-001", dialogs.Markup, StringComparison.Ordinal));
        Assert.Equal(.2m, dialogs.FindComponent<MudNumericField<decimal>>().Instance.GetState(field => field.Value));
        var fields = dialogs.FindComponents<MudTextField<string>>();
        Assert.Equal("Pallet", fields.Single(field => field.Instance.Label == "Đơn vị").Instance.GetState(field => field.Value));
        Assert.Equal("Engineer", fields.Single(field => field.Instance.Label == "Người phê duyệt").Instance.GetState(field => field.Value));
        var dates = dialogs.FindComponents<MudDatePicker>();
        Assert.Equal(new DateTime(2026, 1, 1), dates.Single(date => date.Instance.Label == "Hiệu lực từ ngày").Instance.Date);
        Assert.Equal(new DateTime(2026, 2, 1), dates.Single(date => date.Instance.Label == "Hiệu lực đến ngày (tùy chọn)").Instance.Date);
        dialogs.FindAll("button").Single(button => button.TextContent.Trim() == "Lưu").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Hãy chọn thiết bị hoặc SKU khác", dialogs.Markup, StringComparison.Ordinal));
        Assert.Null(commands.LastCreatedStandard);

        var sku = dialogs.FindComponents<MudSelect<Guid?>>().Single(select => select.Instance.Label == "Sản phẩm (SKU)");
        await sku.InvokeAsync(() => sku.Instance.ValueChanged.InvokeAsync(CopyProductId));
        dialogs.FindAll("button").Single(button => button.TextContent.Trim() == "Lưu").Click();

        dialogs.WaitForAssertion(() => Assert.Empty(dialogs.FindComponents<MasterDataCreateDialog>()));
        Assert.Equal(new CreateStandardCommand(WorkUnitId, CopyProductId, .2m, "Pallet", new DateOnly(2026, 1, 1),
            new DateOnly(2026, 2, 1), "Engineer"), commands.LastCreatedStandard);
        Assert.Null(commands.LastCreatedProduct);
    }

    [Fact]
    public void FailedCopySourceLoadKeepsSaveDisabled()
    {
        Services.AddScoped<IMasterDataQueryService>(_ => new FakeMasterQuery { FailProductDetails = true });
        var commands = new NoOpMasterCommands();
        Services.AddScoped<IMasterDataScenario>(_ => commands).AddScenarioRequests();
        _ = Menus;
        var dialogs = Render<MudDialogProvider>();
        var page = Render<Products>();
        page.WaitForAssertion(() => Assert.Single(page.FindAll(".row-action-menu")));

        SelectAction(page, "Sao chép sản phẩm");

        dialogs.WaitForAssertion(() =>
        {
            Assert.Contains("Không thể tải dữ liệu nguồn", dialogs.Markup, StringComparison.Ordinal);
            Assert.True(dialogs.FindAll("button").Single(button => button.TextContent.Trim() == "Lưu").HasAttribute("disabled"));
        });
        Assert.Null(commands.LastCreatedProduct);
    }

    [Fact]
    public void StandardsExposeEndEffectivenessAndDeleteActionsWithoutDirectSpeedEdit()
    {
        // Arrange & Act: render bảng định mức đang có hiệu lực.
        Services.AddScoped<IMasterDataQueryService>(_ => new FakeMasterQuery());
        Services.AddScoped<IMasterDataScenario>(_ => new NoOpMasterCommands()).AddScenarioRequests();
        var page = Render<WorkUnitStandards>();
        page.WaitForAssertion(() => Assert.Contains("M-001", page.Markup, StringComparison.Ordinal));
        // Assert: chỉ có EndEffectiveness/Delete; tốc độ lịch sử không được sửa trực tiếp.
        OpenActions(page);
        Assert.NotNull(MenuAction("Kết thúc hiệu lực định mức"));
        Assert.NotNull(MenuAction("Xóa định mức"));
        Assert.NotNull(MenuAction("Sao chép định mức"));
        Assert.DoesNotContain("Sửa định mức", Menus.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WorkCenterCreateUsesSearchableActiveAreaAutocomplete()
    {
        // Arrange & Act: mở dialog tạo Group với lookup Area active.
        Services.AddScoped<IMasterDataQueryService>(_ => new FakeMasterQuery());
        _ = Menus;
        var dialogs = Render<MudDialogProvider>();
        var parameters = new DialogParameters<MasterDataCreateDialog> { { x => x.Kind, MasterCreateKind.WorkCenter } };
        await Services.GetRequiredService<IDialogService>().ShowAsync<MasterDataCreateDialog>("Thêm dây chuyền", parameters);
        // Assert: dùng MudAutocomplete và mô tả tìm theo code/name/site thay vì tải dropdown cứng.
        dialogs.WaitForAssertion(() =>
        {
            Assert.Contains("mud-autocomplete", dialogs.Markup, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Tìm theo mã, tên khu vực sản xuất hoặc nhà máy", dialogs.Markup, StringComparison.Ordinal);
        });
    }

    internal sealed class FakeMasterQuery(bool failProducts = false) : IMasterDataQueryService
    {
        public string ProductSourceStatus { get; set; } = "Active";
        public bool FailProductDetails { get; set; }
        public WorkUnitStandardListItem? StandardSource { get; set; }
        public MasterListQuery? LastProductQuery { get; private set; }
        public int ProductQueryCount { get; private set; }
        public Task<MasterDataLookups> LookupsAsync(CancellationToken cancellationToken) => Task.FromResult(Lookups());
        public Task<ProductListItem> ProductAsync(Guid id, CancellationToken cancellationToken) => FailProductDetails
            ? Task.FromException<ProductListItem>(new KeyNotFoundException("Source product missing"))
            : Task.FromResult(new ProductListItem(ProductId, "SKU-001", "Product", "PCS", "Description", ProductSourceStatus, Updated, Token));
        public Task<WorkUnitStandardListItem> StandardAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(
            StandardSource is not null ? StandardSource with { Id = id }
                : new WorkUnitStandardListItem(id, WorkUnitId, "M-001", ProductId, "SKU-001", 10m, 600m, "PCS", new DateOnly(2026, 1, 1), null, "planner", true, Updated, Token));
        public Task<ShiftListItem> ShiftAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(new ShiftListItem(id, SiteId, "SITE-01 — Factory", "S3", "Night shift", new TimeOnly(22, 0), new TimeOnly(6, 0), true, "Active", Updated, Token));
        public Task<DowntimeReasonListItem> DowntimeReasonAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(new DowntimeReasonListItem(id, "DR-01", "Breakdown", "AvailabilityLoss", "Breakdown", null, "Active", false, Updated, Token));
        public Task<WorkUnitListItem> WorkUnitAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(new WorkUnitListItem(id, GroupId, "SITE-01 / PROC-01 / LINE-01", "PROC-01", "SITE-01", "M-001", "Machine", "Running", "Connected", "Active", Updated, Token, "Stored description"));
        public Task<WorkCenterListItem> WorkCenterAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(new WorkCenterListItem(id, AreaId, "SITE-01 / PROC-01 — Area", "SITE-01", "LINE-01", "Line", 1, "Active", Updated, Token, "Stored description"));
        public Task<AreaListItem> AreaAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(new AreaListItem(id, SiteId, "SITE-01 — Factory", "PROC-01", "Area", 1, "Active", Updated, Token, "Stored description"));
        public Task<EnterpriseListItem> EnterpriseAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(new EnterpriseListItem(id, "ENT-01", "Enterprise", "Root", 1, "Active", Updated, Token));
        public Task<SiteListItem> SiteAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(new SiteListItem(id, EnterpriseId, "ENT-01 — Enterprise", "SITE-01", "Factory", "SE Asia Standard Time", 1, 3, "Active", Updated, Token, "Stored description"));
        public Task<PagedResult<ProductListItem>> ProductsAsync(MasterListQuery query, CancellationToken cancellationToken)
        {
            ProductQueryCount++;
            LastProductQuery = query;
            if (failProducts) return Task.FromException<PagedResult<ProductListItem>>(new InvalidOperationException("database unavailable"));
            return Task.FromResult(new PagedResult<ProductListItem>(
                [new(ProductId, "SKU-001", "Product", "unit", "Description", "Active", Updated, Token)], 1));
        }
        public Task<PagedResult<WorkUnitStandardListItem>> StandardsAsync(MasterListQuery query, CancellationToken cancellationToken) => Task.FromResult(new PagedResult<WorkUnitStandardListItem>(
            [new(Guid.Parse("70000000-0000-0000-0000-000000000001"), WorkUnitId, "M-001", ProductId, "SKU-001", 10m, 600m, "PCS", new DateOnly(2026, 1, 1), null, "planner", true, Updated, Token)], 1));
        public Task<PagedResult<ShiftListItem>> ShiftsAsync(MasterListQuery query, CancellationToken cancellationToken) => Task.FromResult(new PagedResult<ShiftListItem>(
            [new(Guid.NewGuid(), SiteId, "SITE-01", "S3", "Night shift", new TimeOnly(22, 0), new TimeOnly(6, 0), true, "Active", Updated, Token)], 1));
        public Task<PagedResult<DowntimeReasonListItem>> DowntimeReasonsAsync(MasterListQuery query, CancellationToken cancellationToken) => Task.FromResult(new PagedResult<DowntimeReasonListItem>([], 0));
        private static string Token => Convert.ToBase64String([1]);
        private static MasterDataLookups Lookups() => new(
            [new(EnterpriseId, "ENT-01", "Enterprise")],
            [new(SiteId, "SITE-01", "Factory", EnterpriseId)],
            [new(AreaId, "PROC-01", "Area", SiteId)],
            [new(GroupId, "LINE-01", "Line", AreaId)],
            [new(WorkUnitId, "M-001", "Machine", GroupId)],
            [new(ProductId, "SKU-001", "Product"), new(CopyProductId, "SKU-NEW", "Destination product")],
            [new(Guid.Empty, "Inactive", "Inactive"), new(Guid.Empty, "Active", "Active")],
            [new(Guid.Empty, "Unknown", "Unknown"), new(Guid.Empty, "AvailabilityLoss", "AvailabilityLoss")],
            [new(Guid.Empty, "Unknown", "Unknown"), new(Guid.Empty, "Breakdown", "Breakdown")],
            ["SE Asia Standard Time", "UTC"]);
    }

    internal sealed class NoOpMasterCommands(bool failProductCreate = false) : IMasterDataScenario
    {
        public EquipmentHierarchyDeleteItem[]? LastBulkDelete { get; private set; }
        public bool FailBulkDelete { get; set; }
        public Task DeleteHierarchyAsync(IReadOnlyCollection<EquipmentHierarchyDeleteItem> items, CancellationToken cancellationToken)
        {
            LastBulkDelete = items.ToArray();
            return FailBulkDelete ? Task.FromException(new InvalidOperationException("password=hidden")) : Task.CompletedTask;
        }
        public CreateProductCommand? LastCreatedProduct { get; private set; }
        public CreateStandardCommand? LastCreatedStandard { get; private set; }
        public Guid? LastHierarchyParentId { get; private set; }
        public string? LastHierarchyCreatedKind { get; private set; }
        public string? LastHierarchyDescription { get; private set; }
        public string? LastHierarchyCode { get; private set; }
        public (Guid Id, string RowVersion)? LastDeletedWorkUnit { get; private set; }
        public UpdateProductCommand? LastProductUpdate { get; private set; }
        public SetProductStatusCommand? LastStatusRequest { get; private set; }
        public int ProductStatusCallCount { get; private set; }
        private static MasterMutationResult Result => new(Guid.NewGuid(), Convert.ToBase64String([1]));
        public Task<MasterMutationResult> CreateProductAsync(CreateProductCommand request, CancellationToken cancellationToken)
        {
            LastCreatedProduct = request;
            return failProductCreate
                ? Task.FromException<MasterMutationResult>(new InvalidOperationException("sql unavailable"))
                : Task.FromResult(Result);
        }
        public Task<MasterMutationResult> UpdateProductAsync(UpdateProductCommand request, CancellationToken cancellationToken) { LastProductUpdate = request; return Task.FromResult(Result); }
        public Task<MasterMutationResult> SetProductStatusAsync(SetProductStatusCommand request, CancellationToken cancellationToken) { LastStatusRequest = request; ProductStatusCallCount++; return Task.FromResult(Result); }
        public Task DeleteProductAsync(Guid id, string rowVersion, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<MasterMutationResult> CreateStandardAsync(CreateStandardCommand request, CancellationToken cancellationToken)
        {
            LastCreatedStandard = request;
            return Task.FromResult(Result);
        }
        public Task<MasterMutationResult> EndStandardEffectivenessAsync(EndStandardEffectivenessCommand request, CancellationToken cancellationToken) => Task.FromResult(Result);
        public Task DeleteStandardAsync(Guid id, string rowVersion, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<MasterMutationResult> CreateShiftAsync(CreateShiftCommand request, CancellationToken cancellationToken) => Task.FromResult(Result);
        public Task<MasterMutationResult> UpdateShiftAsync(UpdateShiftCommand request, CancellationToken cancellationToken) => Task.FromResult(Result);
        public Task<MasterMutationResult> SetShiftStatusAsync(SetShiftStatusCommand request, CancellationToken cancellationToken) => Task.FromResult(Result);
        public Task DeleteShiftAsync(Guid id, string rowVersion, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<MasterMutationResult> CreateDowntimeReasonAsync(CreateDowntimeReasonCommand request, CancellationToken cancellationToken) => Task.FromResult(Result);
        public Task<MasterMutationResult> UpdateDowntimeReasonAsync(UpdateDowntimeReasonCommand request, CancellationToken cancellationToken) => Task.FromResult(Result);
        public Task<MasterMutationResult> SetDowntimeReasonStatusAsync(SetDowntimeReasonStatusCommand request, CancellationToken cancellationToken) => Task.FromResult(Result);
        public Task DeleteDowntimeReasonAsync(Guid id, string rowVersion, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<MasterMutationResult> CreateWorkUnitAsync(CreateWorkUnitCommand request, CancellationToken cancellationToken)
        {
            LastHierarchyParentId = request.WorkCenterId; LastHierarchyCreatedKind = "WorkUnit"; LastHierarchyDescription = request.Description; return Task.FromResult(Result);
        }
        public Task<MasterMutationResult> UpdateWorkUnitAsync(UpdateWorkUnitCommand request, CancellationToken cancellationToken) { LastHierarchyDescription = request.Description; LastHierarchyCode = request.Code; return Task.FromResult(Result); }
        public Task<MasterMutationResult> SetWorkUnitStatusAsync(SetWorkUnitStatusCommand request, CancellationToken cancellationToken) => Task.FromResult(Result);
        public Task DeleteWorkUnitAsync(Guid id, string rowVersion, CancellationToken cancellationToken)
        {
            LastDeletedWorkUnit = (id, rowVersion); return Task.CompletedTask;
        }
        public Task<MasterMutationResult> CreateWorkCenterAsync(CreateWorkCenterCommand request, CancellationToken cancellationToken)
        {
            LastHierarchyParentId = request.AreaId; LastHierarchyCreatedKind = "WorkCenter"; LastHierarchyDescription = request.Description; return Task.FromResult(Result);
        }
        public Task<MasterMutationResult> UpdateWorkCenterAsync(UpdateWorkCenterCommand request, CancellationToken cancellationToken) { LastHierarchyDescription = request.Description; LastHierarchyCode = request.Code; return Task.FromResult(Result); }
        public Task<MasterMutationResult> SetWorkCenterStatusAsync(SetWorkCenterStatusCommand request, CancellationToken cancellationToken) => Task.FromResult(Result);
        public Task DeleteWorkCenterAsync(Guid id, string rowVersion, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<MasterMutationResult> CreateAreaAsync(CreateAreaCommand request, CancellationToken cancellationToken)
        {
            LastHierarchyParentId = request.SiteId; LastHierarchyCreatedKind = "Area"; LastHierarchyDescription = request.Description; return Task.FromResult(Result);
        }
        public Task<MasterMutationResult> UpdateAreaAsync(UpdateAreaCommand request, CancellationToken cancellationToken) { LastHierarchyDescription = request.Description; LastHierarchyCode = request.Code; return Task.FromResult(Result); }
        public Task<MasterMutationResult> SetAreaStatusAsync(SetAreaStatusCommand request, CancellationToken cancellationToken) => Task.FromResult(Result);
        public Task DeleteAreaAsync(Guid id, string rowVersion, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<MasterMutationResult> CreateEnterpriseAsync(CreateEnterpriseCommand request, CancellationToken cancellationToken) { LastHierarchyDescription = request.Description; return Task.FromResult(Result); }
        public Task<MasterMutationResult> UpdateEnterpriseAsync(UpdateEnterpriseCommand request, CancellationToken cancellationToken) { LastHierarchyDescription = request.Description; LastHierarchyCode = request.Code; return Task.FromResult(Result); }
        public Task<MasterMutationResult> SetEnterpriseStatusAsync(SetEnterpriseStatusCommand request, CancellationToken cancellationToken) => Task.FromResult(Result);
        public Task DeleteEnterpriseAsync(Guid id, string rowVersion, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<MasterMutationResult> CreateSiteAsync(CreateSiteCommand request, CancellationToken cancellationToken)
        {
            LastHierarchyParentId = request.EnterpriseId; LastHierarchyCreatedKind = "Site"; LastHierarchyDescription = request.Description; return Task.FromResult(Result);
        }
        public Task<MasterMutationResult> UpdateSiteAsync(UpdateSiteCommand request, CancellationToken cancellationToken) { LastHierarchyDescription = request.Description; LastHierarchyCode = request.Code; return Task.FromResult(Result); }
        public Task<MasterMutationResult> SetSiteStatusAsync(SetSiteStatusCommand request, CancellationToken cancellationToken) => Task.FromResult(Result);
        public Task DeleteSiteAsync(Guid id, string rowVersion, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
