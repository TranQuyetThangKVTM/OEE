using System.Globalization;
using Bunit;
using Bunit.TestDoubles;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.Operations;
using Oee.Application.Modules;
using Oee.Application.Security;
using Oee.Web.Components.Pages;
using Oee.Web.Realtime;
using AlertPage = Oee.Web.Components.Pages.Alerts.Alerts;
using TargetPage = Oee.Web.Components.Pages.Alerts.Targets;

namespace Oee.Web.ComponentTests;

/// <summary>
/// bUnit Test cho cảnh báo SLO, target và Live OEE cùng các capability tương ứng.
/// </summary>
public sealed class AlertsRealtimePageTests : RowMenuTestContext, IAsyncLifetime
{
    private readonly BunitAuthorizationContext authorization;

    /// <summary>Khởi tạo principal Viewer, MudBlazor và fake Application Services cho các page realtime.</summary>
    public AlertsRealtimePageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddAuthorizationCore(options =>
        {
            options.AddPolicy(Capabilities.DashboardRead, policy => policy.RequireAuthenticatedUser());
            options.AddPolicy(Capabilities.AlertsManage, policy => policy.RequireRole("Admin", "PlanningManager", "Operator"));
            options.AddPolicy(Capabilities.SnapshotsManage, policy => policy.RequireRole("Admin", "PlanningManager"));
            options.AddPolicy(Capabilities.TargetsManage, policy => policy.RequireRole("Admin", "PlanningManager"));
        });
        authorization = this.AddAuthorization();
        authorization.SetAuthorized("viewer");
        authorization.SetRoles("Viewer");
        authorization.SetPolicies(Capabilities.DashboardRead);
        Services.AddScoped<IAlertModuleScenario>(_ => new FakeAlertService()).AddScenarioRequests();
        Services.AddScoped<IMasterDataQueryService>(_ => new EmptyMasterData());
    }

    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;
    async Task IAsyncLifetime.DisposeAsync() => await base.DisposeAsync();

    [Fact]
    public void ViewerCanReadAlertsButCannotSeeMutationActions()
    {
        // Arrange & Act: render danh sách cảnh báo bằng quyền DashboardRead của Viewer.
        var component = Render<AlertPage>();
        component.WaitForAssertion(() => Assert.Contains("M-001", component.Markup, StringComparison.Ordinal));
        // Assert: xem được dữ liệu nhưng các command acknowledge/resolve/reevaluate không được render.
        OpenActions(component);
        Assert.NotNull(MenuAction("Xem cảnh báo"));
        Assert.DoesNotContain("Tiếp nhận cảnh báo", Menus.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Giải quyết cảnh báo", Menus.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Đánh giá lại cảnh báo", Menus.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void OperatorCanManageAlertButCannotReevaluateSnapshot()
    {
        // Arrange: nâng principal test lên Operator có AlertsManage nhưng không có SnapshotsManage.
        authorization.SetRoles("Operator");
        authorization.SetPolicies(Capabilities.DashboardRead, Capabilities.AlertsManage);
        // Act & Assert: Operator thao tác vòng đời alert nhưng không được recalculation snapshot.
        var component = Render<AlertPage>();
        OpenActions(component);
        Assert.NotNull(MenuAction("Tiếp nhận cảnh báo"));
        Assert.NotNull(MenuAction("Giải quyết cảnh báo"));
        Assert.DoesNotContain("Đánh giá lại cảnh báo", Menus.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void TargetEditorIsHiddenFromViewerAndShownToPlanner()
    {
        // Arrange & Assert: Viewer chỉ đọc target, không thấy nút tạo version.
        var viewer = Render<TargetPage>();
        viewer.WaitForAssertion(() => Assert.Contains("Toàn cục", viewer.Markup, StringComparison.Ordinal));
        Assert.DoesNotContain("Thêm ngưỡng", viewer.Markup, StringComparison.Ordinal);

        // Act: đổi principal sang PlanningManager và render lại component.
        authorization.SetAuthorized("planner");
        authorization.SetRoles("PlanningManager");
        authorization.SetPolicies(Capabilities.DashboardRead, Capabilities.TargetsManage,
            Capabilities.SnapshotsManage, Capabilities.AlertsManage);
        // Assert: capability TargetsManage làm xuất hiện action quản trị.
        var planner = Render<TargetPage>();
        planner.WaitForAssertion(() => Assert.Contains("Thêm ngưỡng", planner.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public void LivePageUsesSharedRealtimeStateAndKeepsConnectivitySeparateFromOperationalState()
    {
        // Arrange: state chứa một máy Stopped+Disconnected và một máy Stopped+Connected.
        var state = CreateState();
        Services.AddSingleton(state);
        // Act & Assert: UI hiển thị mất kết nối tách biệt với trạng thái vận hành và có source timestamp.
        var component = Render<LiveOee>();
        component.WaitForAssertion(() =>
        {
            Assert.Contains("MẤT KẾT NỐI", component.Markup, StringComparison.Ordinal);
            Assert.Contains("PLAN-001", component.Markup, StringComparison.Ordinal);
            Assert.Contains("Nhận OPC", component.Markup, StringComparison.Ordinal);
        });

        // Act & Assert: state reconnecting đẩy lỗi mới tới component dùng chung mà không polling dày database.
        state.MarkReconnecting("database unavailable", DateTimeOffset.UtcNow);
        component.WaitForAssertion(() => Assert.Contains("database unavailable", component.Markup, StringComparison.Ordinal));
    }

    private static RealtimeOeeState CreateState()
    {
        // Dữ liệu giả chỉ phục vụ bUnit; production nhận cùng LiveOeeModel từ typed SignalR contract.
        var at = new DateTimeOffset(2026, 8, 2, 8, 0, 0, TimeSpan.Zero);
        var state = new RealtimeOeeState();
        state.Publish(new LiveOeeModel(
            [
                new(Guid.NewGuid(), "M-001", "Machine offline", "Stopped", "Disconnected", .7m, .8m, .9m, .98m, 10, 9, 1, 100, 100, "PLAN-001", "SKU-01", at, at, null, null, false, 1),
                new(Guid.NewGuid(), "M-002", "Machine stopped", "Stopped", "Connected", .8m, .9m, .9m, .99m, 20, 20, 0, 100, 200, "PLAN-002", "SKU-01", at, at, at.AddMinutes(-5), "DR-01 · Breakdown", false, 1),
            ],
            new(2, 0, 1, 1, 1, .75m, .85m, .9m, .985m, 30, 29, 1, 200, 1),
            "S1 · Sáng", at.AddHours(-2), at.AddHours(6), .25m, "DR-01 · Breakdown", at, at));
        return state;
    }

    [Fact]
    public void LiveCardShowsPeriodQuantitiesWithoutDetailOrOldProductionTiles()
    {
        var model = CreateState().Current!;
        var machine = model.Machines.First() with
        {
            ProductionPeriods = new("S3", new(2026, 10, 13), 42, 171422, 180000, 555564, 2270837, 160000)
        };
        var component = Render<LiveOeeBoard>(parameters => parameters.Add(x => x.Model, model with { Machines = [machine] }));
        Assert.Contains("CA S3", component.Markup, StringComparison.Ordinal);
        Assert.Contains("NGÀY 13/10", component.Markup, StringComparison.Ordinal);
        Assert.Contains("TUẦN 42", component.Markup, StringComparison.Ordinal);
        Assert.Contains("THÁNG 10", component.Markup, StringComparison.Ordinal);
        Assert.Contains(171422m.ToString("N0", CultureInfo.CurrentCulture), component.Markup, StringComparison.Ordinal);
        Assert.Contains(2270837m.ToString("N0", CultureInfo.CurrentCulture), component.Markup, StringComparison.Ordinal);
        Assert.Contains("SL SKU này:", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("MỤC TIÊU KẾ HOẠCH", component.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("SẢN PHẨM LỖI", component.Markup, StringComparison.Ordinal);
        Assert.Empty(component.FindAll("a"));
    }

    [Theory]
    [InlineData("Connected", "Running", false, "ĐANG CHẠY", "running")]
    [InlineData("Connected", "Stopped", false, "ĐANG DỪNG", "stopped")]
    [InlineData("Disconnected", "Running", false, "MẤT KẾT NỐI", "offline")]
    [InlineData("Connected", "Running", true, "DỮ LIỆU CHƯA CẬP NHẬT", "offline")]
    [InlineData("NoData", "Running", false, "KHÔNG CÓ DỮ LIỆU", "offline")]
    [InlineData("NotConfigured", "Running", false, "CHƯA CẤU HÌNH", "unconfigured")]
    [InlineData("Connected", "Maintenance", false, "ĐANG BẢO TRÌ", "waiting")]
    public void LiveCardUsesDistinctStatusAndNeverReportsRunningForUnavailableData(
        string connectivity, string operational, bool stale, string label, string cardState)
    {
        var machine = CreateState().Current!.Machines.First() with
        {
            ConnectivityState = connectivity, OperationalState = operational, IsStale = stale
        };
        var component = Render<LiveMachineCard>(parameters => parameters.Add(x => x.Machine, machine));
        var status = component.Find("[data-card-state]");
        Assert.Equal(label, status.TextContent.Trim());
        Assert.Equal(cardState, status.GetAttribute("data-card-state"));
        Assert.Contains($"live-machine-card--{cardState}", component.Find(".live-machine-card").ClassList);
        if (connectivity != "Connected" || stale)
            Assert.DoesNotContain("ĐANG CHẠY", status.TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void LiveCardKeepsConfirmedShiftQuantityWithoutCollectionGapBanner()
    {
        var machine = CreateState().Current!.Machines.First() with
        {
            UnassignedGapQuantity = 5000,
            LastCollectionGapAt = DateTimeOffset.UtcNow,
            ProductionPeriods = new("S01", new(2026, 10, 31), 44, 17, 17, 17, 17, 17)
        };
        var component = Render<LiveMachineCard>(parameters => parameters.Add(item => item.Machine, machine));
        Assert.Empty(component.FindAll("[data-collection-gap]"));
        Assert.DoesNotContain("chưa xác định ca", component.Markup, StringComparison.Ordinal);
        Assert.Equal(17m, component.Instance.Machine.ProductionPeriods!.ShiftQuantity);
    }

    [Fact]
    public void LiveCardDistinguishesUndefinedQualityFromMeasuredZeroPerformance()
    {
        var model = CreateState().Current!;
        var machine = model.Machines.First() with
        {
            CurrentPlan = "P-TEST", Oee = null, Availability = 1, Performance = 0, Quality = null
        };
        var component = Render<LiveOeeBoard>(parameters => parameters.Add(x => x.Model,
            model with { Machines = [machine] }));
        Assert.Equal("—", component.Find("[data-metric='oee']").TextContent.Trim());
        Assert.Equal("—", component.Find("[data-metric='quality']").TextContent.Trim());
        Assert.DoesNotContain("—", component.Find("[data-metric='performance']").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void LiveCardWarnsBelowRunningStateAndClearsWhenCounterResumes()
    {
        var model = CreateState().Current!;
        var machine = model.Machines.First() with
        {
            OperationalState = "Running", ConnectivityState = "Connected", IsStale = false,
            IsCounterStalled = true, CounterIdleSeconds = 17520
        };
        var component = Render<LiveOeeBoard>(parameters => parameters.Add(x => x.Model, model with { Machines = [machine] }));
        Assert.Contains("ĐANG CHẠY", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Bộ đếm chưa thay đổi", component.Find(".mud-alert").TextContent, StringComparison.Ordinal);
        Assert.Contains("4h52m", component.Find(".mud-alert").TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Dữ liệu không cập nhật", component.Find(".mud-alert").TextContent, StringComparison.Ordinal);
        Assert.Equal("Running", component.Find("[data-machine-state]").GetAttribute("data-machine-state"));
        var bannerPosition = component.Markup.IndexOf("ĐANG CHẠY", StringComparison.Ordinal);
        var warningPosition = component.Markup.IndexOf("Bộ đếm chưa thay đổi", StringComparison.Ordinal);
        var planPosition = component.Markup.IndexOf("Kế hoạch:", StringComparison.Ordinal);
        Assert.True(bannerPosition < warningPosition && warningPosition < planPosition);
        component.Render(parameters => parameters.Add(x => x.Model,
            model with { Machines = [machine with { IsCounterStalled = false, CounterIdleSeconds = 0 }] }));
        Assert.Empty(component.FindAll(".mud-alert"));
    }

    [Fact]
    public void LiveCardKeepsCheckpointTimestampsWithoutCounterRecoveryBanner()
    {
        var received = DateTimeOffset.UtcNow;
        var machine = CreateState().Current!.Machines.First() with
        {
            OperationalState = "Running", ConnectivityState = "Connected", IsStale = false,
            IsCounterStalled = false, CounterDiscontinuityPending = true,
            LastDataAt = received, CounterAcceptedAt = received.AddDays(-5)
        };
        var component = Render<LiveMachineCard>(parameters => parameters.Add(x => x.Machine, machine));
        Assert.Empty(component.FindAll(".mud-alert"));
        Assert.DoesNotContain("Bộ đếm chưa thay đổi", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Counter chấp nhận", component.Markup, StringComparison.Ordinal);
        Assert.Contains("Nhận OPC", component.Markup, StringComparison.Ordinal);
        Assert.Empty(component.FindAll("a"));
        Assert.Equal("Running", component.Find("[data-machine-state]").GetAttribute("data-machine-state"));
    }

    [Fact]
    public void TargetVersionEditorUsesModalAndConvertsPercentageBeforeSaving()
    {
        var service = new FakeAlertService();
        Services.AddScoped<IAlertModuleScenario>(_ => service);
        authorization.SetAuthorized("planner");
        authorization.SetRoles("PlanningManager");
        authorization.SetPolicies(Capabilities.DashboardRead, Capabilities.TargetsManage);
        _ = Menus;
        var dialogs = Render<MudDialogProvider>();
        var page = Render<TargetPage>();
        SelectAction(page, "Thêm phiên bản mục tiêu");
        dialogs.WaitForAssertion(() => Assert.Single(dialogs.FindComponents<Oee.Web.Components.Pages.Alerts.OeeTargetEditorDialog>()));
        Assert.Empty(page.FindComponents<Oee.Web.Components.Pages.Alerts.OeeTargetEditorDialog>());
        var oee = dialogs.FindComponents<MudNumericField<decimal>>().Single(x => x.Instance.Label == "OEE (%)");
        Assert.Equal(75m, decimal.Parse(oee.Find("input").GetAttribute("value")!, CultureInfo.CurrentCulture));
        oee.Find("input").Change("85");
        service.FailTargetSave = true;
        dialogs.FindAll("button").Single(x => x.TextContent.Trim() == "Lưu phiên bản").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Không thể lưu mục tiêu", dialogs.Markup, StringComparison.Ordinal));
        Assert.Single(dialogs.FindComponents<Oee.Web.Components.Pages.Alerts.OeeTargetEditorDialog>());
        service.FailTargetSave = false;
        dialogs.FindAll("button").Single(x => x.TextContent.Trim() == "Lưu phiên bản").Click();
        dialogs.WaitForAssertion(() => Assert.Empty(dialogs.FindComponents<Oee.Web.Components.Pages.Alerts.OeeTargetEditorDialog>()));
        var request = service.LastTargetRequest!;
        Assert.Equal(.85m, request.MinimumOee);
        Assert.Equal(.99m, request.MinimumQuality);
        Assert.Equal("DAY", request.PeriodCode);
    }

    private sealed class FakeAlertService : IAlertModuleScenario
    {
        private static readonly DateTimeOffset At = new(2026, 8, 2, 8, 0, 0, TimeSpan.Zero);
        private static readonly AlertListItem Alert = new(Guid.NewGuid(), Guid.NewGuid(), "M-001", "Blow Moulder", "SHIFT-S1-20260802", "Shift", "Oee", "Critical", "Open", .6m, .8m, -.2m, At, null, null, null, null, At.ToString("O"));
        private static readonly TargetListItem Target = new(Guid.NewGuid(), null, null, "Global", "Day", "DAY", .75m, .85m, .9m, .99m, new(2026, 1, 1), null, "baseline", true, Convert.ToBase64String([1]));
        public Task<PagedResult<AlertListItem>> ListAsync(AlertListQuery query, CancellationToken cancellationToken) => Task.FromResult(new PagedResult<AlertListItem>([Alert], 1));
        public Task<PagedResult<TargetListItem>> TargetsAsync(TargetListQuery query, CancellationToken cancellationToken) => Task.FromResult(new PagedResult<TargetListItem>([Target], 1));
        public Task AcknowledgeAsync(Guid id, AlertMutationRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ResolveAsync(Guid id, AlertMutationRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ReevaluateAsync(Guid id, string concurrencyToken, CancellationToken cancellationToken) => Task.CompletedTask;
        public bool FailTargetSave { get; set; }
        public EffectiveTargetRequest? LastTargetRequest { get; private set; }
        public Task<Guid> CreateTargetVersionAsync(EffectiveTargetRequest request, CancellationToken cancellationToken) { LastTargetRequest = request; if (FailTargetSave) throw new InvalidOperationException("test conflict"); return Task.FromResult(Guid.NewGuid()); }
        public Task EndTargetAsync(Guid id, EndTargetRequest request, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task DeleteTargetAsync(Guid id, string rowVersion, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class EmptyMasterData : IMasterDataQueryService
    {
        private static readonly MasterDataLookups EmptyLookups = new([], [], [], [], [], [], [], [], [], []);
        public Task<MasterDataLookups> LookupsAsync(CancellationToken cancellationToken) => Task.FromResult(EmptyLookups);
        public Task<ProductListItem> ProductAsync(Guid id, CancellationToken cancellationToken) => throw new KeyNotFoundException();
        public Task<WorkUnitStandardListItem> StandardAsync(Guid id, CancellationToken cancellationToken) => throw new KeyNotFoundException();
        public Task<ShiftListItem> ShiftAsync(Guid id, CancellationToken cancellationToken) => throw new KeyNotFoundException();
        public Task<DowntimeReasonListItem> DowntimeReasonAsync(Guid id, CancellationToken cancellationToken) => throw new KeyNotFoundException();
        public Task<WorkUnitListItem> WorkUnitAsync(Guid id, CancellationToken cancellationToken) => throw new KeyNotFoundException();
        public Task<WorkCenterListItem> WorkCenterAsync(Guid id, CancellationToken cancellationToken) => throw new KeyNotFoundException();
        public Task<AreaListItem> AreaAsync(Guid id, CancellationToken cancellationToken) => throw new KeyNotFoundException();
        public Task<EnterpriseListItem> EnterpriseAsync(Guid id, CancellationToken cancellationToken) => throw new KeyNotFoundException();
        public Task<SiteListItem> SiteAsync(Guid id, CancellationToken cancellationToken) => throw new KeyNotFoundException();
        public Task<PagedResult<ProductListItem>> ProductsAsync(MasterListQuery query, CancellationToken cancellationToken) => Empty<ProductListItem>();
        public Task<PagedResult<WorkUnitStandardListItem>> StandardsAsync(MasterListQuery query, CancellationToken cancellationToken) => Empty<WorkUnitStandardListItem>();
        public Task<PagedResult<ShiftListItem>> ShiftsAsync(MasterListQuery query, CancellationToken cancellationToken) => Empty<ShiftListItem>();
        public Task<PagedResult<DowntimeReasonListItem>> DowntimeReasonsAsync(MasterListQuery query, CancellationToken cancellationToken) => Empty<DowntimeReasonListItem>();
        private static Task<PagedResult<T>> Empty<T>() => Task.FromResult(new PagedResult<T>([], 0));
    }
}
