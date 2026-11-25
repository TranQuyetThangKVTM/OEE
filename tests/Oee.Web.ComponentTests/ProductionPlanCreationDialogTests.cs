using System.Reflection;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Oee.Application.Common;
using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.Operations;
using Oee.Application.Modules;
using Oee.Application.Production.Plans.Create;
using Oee.Application.Production.Plans.PreviewBulk;
using Oee.Application.Security;
using Oee.Web.Components.Pages.Production;

namespace Oee.Web.ComponentTests;

public sealed class ProductionPlanCreationDialogTests : BunitContext, IAsyncLifetime
{
    private readonly Recorder _recorder = new();
    private readonly Action _setViewer;
    public ProductionPlanCreationDialogTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddScoped<ProductionPlanCreationRunner>();
        Services.AddScoped<Oee.Web.Security.ScopedPrincipalAccessor>();
        Services.AddScoped<ScopeStamp>();
        Services.AddAuthorizationCore(options => options.AddPolicy(Capabilities.ProductionPlanManage,
            policy => policy.RequireRole(RoleCapabilityMatrix.PlanningManager)));
        var auth = this.AddAuthorization();
        auth.SetAuthorized("planner");
        auth.SetRoles(RoleCapabilityMatrix.PlanningManager);
        auth.SetPolicies(Capabilities.ProductionPlanManage);
        _setViewer = () =>
        {
            auth.SetAuthorized("viewer");
            auth.SetRoles(RoleCapabilityMatrix.Viewer);
            auth.SetPolicies();
        };
        Services.AddSingleton<IUseCaseExecutor>(_recorder);
        Services.AddScoped<IProductionModuleQueryService>(provider =>
        {
            _recorder.QueryScopes.Add(provider.GetRequiredService<ScopeStamp>().Id);
            _recorder.QueryUsers.Add(provider.GetRequiredService<Oee.Web.Security.ScopedPrincipalAccessor>().User?.Identity?.Name);
            var proxy = DispatchProxy.Create<IProductionModuleQueryService, QueryProxy>();
            ((QueryProxy)(object)proxy).Recorder = _recorder;
            return proxy;
        });
        Render<MudPopoverProvider>();
    }

    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;
    async Task IAsyncLifetime.DisposeAsync() => await base.DisposeAsync();

    [Fact]
    public async Task RunnerUsesFreshScopesWithCircuitPrincipalAndDoesNotChangeParentScope()
    {
        var root = Services.GetRequiredService<ScopeStamp>().Id;
        var runner = Services.GetRequiredService<ProductionPlanCreationRunner>();
        await runner.LookupsAsync(default);
        await runner.LookupsAsync(default);
        Assert.Equal(2, _recorder.QueryScopes.Distinct().Count());
        Assert.DoesNotContain(root, _recorder.QueryScopes);
        Assert.All(_recorder.QueryUsers, user => Assert.Equal("planner", user));
        Assert.Null(Services.GetRequiredService<Oee.Web.Security.ScopedPrincipalAccessor>().User);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EachPageButtonOpensOnlyItsOwnDialogWithoutModeTabs(bool fromSample)
    {
        var dialogs = Render<MudDialogProvider>();
        var page = Render<ProductionPlans>();
        page.FindAll("button").Single(button => button.TextContent.Trim() == (fromSample ? "Từ kế hoạch mẫu" : "Tạo kế hoạch")).Click();
        dialogs.WaitForAssertion(() =>
        {
            Assert.Empty(dialogs.FindComponents<MudTabs>());
            if (fromSample)
            {
                Assert.Single(dialogs.FindComponents<ProductionPlanFromSampleDialog>());
                Assert.Empty(dialogs.FindComponents<ProductionPlanCreateDialog>());
                Assert.Single(dialogs.FindComponents<MudAutocomplete<SamplePlanListItem>>());
                Assert.Empty(dialogs.FindComponents<MudSelect<Guid>>());
            }
            else
            {
                Assert.Single(dialogs.FindComponents<ProductionPlanCreateDialog>());
                Assert.Empty(dialogs.FindComponents<ProductionPlanFromSampleDialog>());
                Assert.Empty(dialogs.FindComponents<MudAutocomplete<SamplePlanListItem>>());
                Assert.Equal(2, dialogs.FindComponents<MudSelect<Guid>>().Count);
            }
        });
    }

    [Fact]
    public async Task DirectFormValidatesRequiredFieldsAndSavesOnlyDirectInputs()
    {
        var dialogs = Render<MudDialogProvider>();
        await Services.GetRequiredService<IDialogService>().ShowAsync<ProductionPlanCreateDialog>("Direct");
        dialogs.FindAll("button").Single(button => button.TextContent.Trim() == "Lưu").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Hãy chọn thiết bị", dialogs.Markup, StringComparison.Ordinal));
        Assert.Empty(_recorder.Previews);
        await FillDirectAsync(dialogs);
        dialogs.FindAll("button").Single(button => button.TextContent.Trim() == "Lưu").Click();
        dialogs.WaitForAssertion(() => Assert.Single(_recorder.Creates));
        var saved = Assert.Single(_recorder.Creates);
        Assert.Equal(BulkPlanCreationModeContract.Direct, saved.Mode);
        Assert.Equal(new[] { Recorder.UnitId }, saved.WorkUnitIds);
        Assert.Equal(new[] { Recorder.ShiftId }, saved.ShiftIds);
        Assert.Equal(Recorder.ProductId, saved.ProductId);
        Assert.Equal(new DateOnly(2026, 10, 29), saved.AppliedDate);
        Assert.Equal(1234m, saved.PlannedQuantity);
        Assert.Null(saved.SamplePlanId);
        Assert.True(saved.QuantityOverrides is null or { Count: 0 });
        Assert.Single(_recorder.Previews);
    }

    [Fact]
    public async Task SampleFormPreviewsOnSelectionAndDateChangeThenSavesOnlySampleInputsAndOverride()
    {
        var dialogs = Render<MudDialogProvider>();
        await Services.GetRequiredService<IDialogService>().ShowAsync<ProductionPlanFromSampleDialog>("From sample");
        Assert.True(CreateButton(dialogs).HasAttribute("disabled"));
        var sample = dialogs.FindComponent<MudAutocomplete<SamplePlanListItem>>();
        await dialogs.InvokeAsync(() => sample.Instance.ValueChanged.InvokeAsync(_recorder.Sample));
        dialogs.WaitForAssertion(() => Assert.Single(dialogs.FindComponents<MudTable<BulkPlanCombination>>()));
        var date = dialogs.FindComponent<MudDatePicker>();
        var changedDate = _recorder.Previews[0].AppliedDate.AddDays(2);
        await dialogs.InvokeAsync(() => date.Instance.DateChanged.InvokeAsync(changedDate.ToDateTime(TimeOnly.MinValue)));
        Assert.Equal(changedDate, _recorder.Previews[^1].AppliedDate);
        Assert.Contains(FormattableString.Invariant($"{changedDate.AddDays(1):dd/MM} 06:00"), dialogs.Markup, StringComparison.Ordinal);
        var quantity = dialogs.FindComponent<MudNumericField<decimal>>();
        await dialogs.InvokeAsync(() => quantity.Instance.ValueChanged.InvokeAsync(777m));
        CreateButton(dialogs).Click();
        dialogs.WaitForAssertion(() => Assert.Single(_recorder.Creates));
        var saved = Assert.Single(_recorder.Creates);
        Assert.Equal(BulkPlanCreationModeContract.FromSample, saved.Mode);
        Assert.Equal(_recorder.Sample.Id, saved.SamplePlanId);
        Assert.Equal(changedDate, saved.AppliedDate);
        Assert.Empty(saved.WorkUnitIds);
        Assert.Empty(saved.ShiftIds);
        Assert.Null(saved.ProductId);
        Assert.Null(saved.PlannedQuantity);
        Assert.Equal(777m, Assert.Single(saved.QuantityOverrides!).PlannedQuantity);
        // Save kiểm tra lại thay vì tin bản preview đã hiển thị trước đó.
        Assert.Equal(3, _recorder.Previews.Count);
    }

    [Fact]
    public async Task ClearingSampleInvalidatesPreviewAndCannotCreate()
    {
        var dialogs = Render<MudDialogProvider>();
        await Services.GetRequiredService<IDialogService>().ShowAsync<ProductionPlanFromSampleDialog>("From sample");
        var sample = dialogs.FindComponent<MudAutocomplete<SamplePlanListItem>>();
        await dialogs.InvokeAsync(() => sample.Instance.ValueChanged.InvokeAsync(_recorder.Sample));
        await dialogs.InvokeAsync(() => sample.Instance.ValueChanged.InvokeAsync(null));
        Assert.Empty(dialogs.FindComponents<MudTable<BulkPlanCombination>>());
        Assert.True(CreateButton(dialogs).HasAttribute("disabled"));
        Assert.Empty(_recorder.Creates);
    }

    [Fact]
    public async Task RapidDateChangeCancelsOldPreviewAndKeepsLatestDate()
    {
        _recorder.BlockFirstPreview = true;
        var dialogs = Render<MudDialogProvider>();
        await Services.GetRequiredService<IDialogService>().ShowAsync<ProductionPlanFromSampleDialog>("From sample");
        var sample = dialogs.FindComponent<MudAutocomplete<SamplePlanListItem>>();
        var first = dialogs.InvokeAsync(() => sample.Instance.ValueChanged.InvokeAsync(_recorder.Sample));
        dialogs.WaitForAssertion(() => Assert.Single(_recorder.Previews));
        var date = dialogs.FindComponent<MudDatePicker>();
        var changedDate = _recorder.Previews[0].AppliedDate.AddDays(4);
        var second = dialogs.InvokeAsync(() => date.Instance.DateChanged.InvokeAsync(changedDate.ToDateTime(TimeOnly.MinValue)));
        await Task.WhenAll(first, second);
        Assert.True(_recorder.PreviewTokens[0].IsCancellationRequested);
        Assert.Equal(changedDate, _recorder.Previews[^1].AppliedDate);
        dialogs.WaitForAssertion(() =>
        {
            Assert.Contains(FormattableString.Invariant($"{changedDate.AddDays(1):dd/MM} 06:00"), dialogs.Markup, StringComparison.Ordinal);
            Assert.False(CreateButton(dialogs).HasAttribute("disabled"));
        });
        Assert.Empty(_recorder.Creates);
    }

    [Fact]
    public async Task SaveDoesNotCreateWhenFreshPreviewFindsEveryRowAlreadyExists()
    {
        _recorder.ExistingAfterFirstPreview = true;
        var dialogs = Render<MudDialogProvider>();
        await Services.GetRequiredService<IDialogService>().ShowAsync<ProductionPlanFromSampleDialog>("From sample");
        var sample = dialogs.FindComponent<MudAutocomplete<SamplePlanListItem>>();
        await dialogs.InvokeAsync(() => sample.Instance.ValueChanged.InvokeAsync(_recorder.Sample));
        CreateButton(dialogs).Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Không còn dòng hợp lệ", dialogs.Markup, StringComparison.Ordinal));
        Assert.Empty(_recorder.Creates);
        Assert.Single(dialogs.FindComponents<ProductionPlanFromSampleDialog>());
    }

    [Fact]
    public async Task ViewerCannotSubmitEvenIfDialogIsOpenedProgrammatically()
    {
        _setViewer();
        var dialogs = Render<MudDialogProvider>();
        await Services.GetRequiredService<IDialogService>().ShowAsync<ProductionPlanCreateDialog>("Direct");
        await FillDirectAsync(dialogs);
        dialogs.FindAll("button").Single(button => button.TextContent.Trim() == "Lưu").Click();
        dialogs.WaitForAssertion(() => Assert.Contains("Bạn không có quyền", dialogs.Markup, StringComparison.Ordinal));
        Assert.Empty(_recorder.Previews);
        Assert.Empty(_recorder.Creates);
    }

    [Fact]
    public async Task CancelWhilePreviewIsLoadingCancelsReadAndDoesNotCreatePlans()
    {
        _recorder.BlockFirstPreview = true;
        var dialogs = Render<MudDialogProvider>();
        var reference = await Services.GetRequiredService<IDialogService>().ShowAsync<ProductionPlanFromSampleDialog>("From sample");
        var sample = dialogs.FindComponent<MudAutocomplete<SamplePlanListItem>>();
        var loading = dialogs.InvokeAsync(() => sample.Instance.ValueChanged.InvokeAsync(_recorder.Sample));
        dialogs.WaitForAssertion(() => Assert.Single(_recorder.PreviewTokens));
        dialogs.FindAll("button").Single(button => button.TextContent.Trim() == "Hủy").Click();
        await loading;
        Assert.True((await reference.Result)!.Canceled);
        Assert.True(_recorder.PreviewTokens[0].IsCancellationRequested);
        Assert.Empty(_recorder.Creates);
    }

    private static AngleSharp.Dom.IElement CreateButton(IRenderedComponent<MudDialogProvider> dialogs) =>
        dialogs.FindAll("button").Single(button => button.TextContent.Trim() == "Tạo kế hoạch");

    private static async Task FillDirectAsync(IRenderedComponent<MudDialogProvider> dialogs)
    {
        var units = dialogs.FindComponents<MudSelect<Guid>>().Single(field => field.Instance.Label == "Thiết bị / dây chuyền");
        var shifts = dialogs.FindComponents<MudSelect<Guid>>().Single(field => field.Instance.Label == "Ca làm việc");
        await dialogs.InvokeAsync(() => units.Instance.SelectedValuesChanged.InvokeAsync(new[] { Recorder.UnitId }));
        await dialogs.InvokeAsync(() => shifts.Instance.SelectedValuesChanged.InvokeAsync(new[] { Recorder.ShiftId }));
        await dialogs.InvokeAsync(() => dialogs.FindComponent<MudSelect<Guid?>>().Instance.ValueChanged.InvokeAsync(Recorder.ProductId));
        await dialogs.InvokeAsync(() => dialogs.FindComponent<MudDatePicker>().Instance.DateChanged.InvokeAsync(new DateTime(2026, 10, 29)));
        await dialogs.InvokeAsync(() => dialogs.FindComponent<MudNumericField<decimal?>>().Instance.ValueChanged.InvokeAsync(1234m));
    }

    private sealed class Recorder : IUseCaseExecutor
    {
        public static Guid UnitId { get; } = Guid.NewGuid();
        public static Guid ShiftId { get; } = Guid.NewGuid();
        public static Guid ProductId { get; } = Guid.NewGuid();
        public SamplePlanListItem Sample { get; } = new(Guid.NewGuid(), "KHM-0001", "SKU_1", null, 1, 1, 1, 288000, DateTimeOffset.UtcNow, "AQ==");
        public List<BulkProductionPlanRequest> Previews { get; } = [];
        public List<BulkProductionPlanRequest> Creates { get; } = [];
        public List<CancellationToken> PreviewTokens { get; } = [];
        public List<Guid> QueryScopes { get; } = [];
        public List<string?> QueryUsers { get; } = [];
        public bool BlockFirstPreview { get; set; }
        public bool ExistingAfterFirstPreview { get; set; }
        private readonly TaskCompletionSource<bool> _firstPreview = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Guid _sampleItemId = Guid.NewGuid();

        public async Task<T> ExecuteAsync<T>(IRequest<T> request, CancellationToken cancellationToken = default)
        {
            if (request is PreviewProductionPlansQuery preview)
            {
                Previews.Add(preview.Request);
                PreviewTokens.Add(cancellationToken);
                if (BlockFirstPreview && Previews.Count == 1) await _firstPreview.Task.WaitAsync(cancellationToken);
                var row = Row(preview.Request);
                var exists = ExistingAfterFirstPreview && Previews.Count > 1;
                if (exists) row = row with { State = "Skipped", Reason = "Đã có kế hoạch." };
                return (T)(object)new BulkProductionPlanPreview(1, exists ? 0 : 1, exists ? 1 : 0, 0, [row]);
            }
            if (request is CreateProductionPlansCommand create)
            {
                Creates.Add(create.Request);
                return (T)(object)new BulkProductionPlanResult(1, 1, 0, 0, [Guid.NewGuid()], [Row(create.Request)]);
            }
            throw new InvalidOperationException("Unexpected request");
        }

        private BulkPlanCombination Row(BulkProductionPlanRequest request)
        {
            var start = new DateTimeOffset(request.AppliedDate.ToDateTime(new TimeOnly(22, 0)), TimeSpan.FromHours(7));
            return new(UnitId, "M01", ShiftId, "S3", ProductId, "SKU01", 288000, start, start.AddHours(8),
                request.Mode == BulkPlanCreationModeContract.FromSample ? _sampleItemId : null, "Valid", null);
        }
        public Task ExecuteAsync(ICommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class ScopeStamp { public Guid Id { get; } = Guid.NewGuid(); }

    public class QueryProxy : DispatchProxy
    {
        private Recorder State => (Recorder)Recorder;
        public object Recorder { get; set; } = default!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
        {
            "LookupsAsync" => Task.FromResult(new ProductionLookups([new(ProductionPlanCreationDialogTests.Recorder.UnitId, "M01", "Machine")], [],
                [new(ProductionPlanCreationDialogTests.Recorder.ProductId, "SKU01", "Product")],
                [new(ProductionPlanCreationDialogTests.Recorder.ShiftId, "S3", "Ca đêm", StartTime: new(22, 0), EndTime: new(6, 0))], [])),
            "PlansAsync" => Task.FromResult(new PagedResult<ProductionPlanListItem>([], 0)),
            "SamplesAsync" => Task.FromResult(new PagedResult<SamplePlanListItem>([State.Sample], 1)),
            _ => throw new InvalidOperationException("Unexpected production query")
        };
    }
}
