using System.Reflection;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Oee.Application.Contracts.Modules;
using Oee.Application.Modules;
using Oee.Application.Security;
using Oee.Web.Components.Pages.Production;

namespace Oee.Web.ComponentTests;

public sealed class ProductionPlansLoadingTests : BunitContext, IAsyncLifetime
{
    public ProductionPlansLoadingTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddAuthorizationCore(options => options.AddPolicy(Capabilities.ProductionPlanManage,
            policy => policy.RequireRole("Admin")));
        this.AddAuthorization().SetAuthorized("viewer");
        Services.AddScenarioRequests();
    }

    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;
    async Task IAsyncLifetime.DisposeAsync() => await base.DisposeAsync();

    [Fact]
    public async Task PlanTableWaitsForSlowLookupsBeforeUsingTheSameQueryService()
    {
        var query = DispatchProxy.Create<IProductionModuleQueryService, SlowQueries>();
        var proxy = (SlowQueries)(object)query;
        Services.AddSingleton(query);
        var page = Render<ProductionPlans>();

        Assert.Empty(page.FindAll("table"));
        Assert.Equal(0, proxy.PlanCalls);
        await page.InvokeAsync(() => proxy.Lookups.SetResult(new ProductionLookups([], [], [], [], [])));
        page.WaitForAssertion(() =>
        {
            Assert.Single(page.FindAll("table"));
            Assert.Equal(1, proxy.PlanCalls);
        });
        Assert.False(proxy.Overlapped);
    }

    [Fact]
    public void RefreshRetriesFailedLookupsAndTableSuccessDoesNotHideLookupError()
    {
        var query = DispatchProxy.Create<IProductionModuleQueryService, SlowQueries>();
        var proxy = (SlowQueries)(object)query;
        proxy.FailFirstLookup = true;
        proxy.Lookups.SetResult(new ProductionLookups([], [], [], [], []));
        Services.AddSingleton(query);
        var page = Render<ProductionPlans>();
        page.WaitForAssertion(() =>
        {
            Assert.Equal(1, proxy.PlanCalls);
            Assert.Contains("Không thể tải bộ lọc", page.Markup, StringComparison.Ordinal);
        });
        page.FindAll("button").Single(button => button.TextContent.Trim() == "Làm mới").Click();
        page.WaitForAssertion(() =>
        {
            Assert.Equal(2, proxy.LookupCalls);
            Assert.Equal(2, proxy.PlanCalls);
            Assert.DoesNotContain("Không thể tải bộ lọc", page.Markup, StringComparison.Ordinal);
        });
    }

    public class SlowQueries : DispatchProxy
    {
        public TaskCompletionSource<ProductionLookups> Lookups { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int LookupCalls { get; private set; }
        public int PlanCalls { get; private set; }
        public bool Overlapped { get; private set; }
        public bool FailFirstLookup { get; set; }

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method?.Name == nameof(IProductionModuleQueryService.LookupsAsync))
            {
                LookupCalls++;
                return FailFirstLookup && LookupCalls == 1
                    ? Task.FromException<ProductionLookups>(new InvalidOperationException("Lookup unavailable"))
                    : Lookups.Task;
            }
            if (method?.Name == nameof(IProductionModuleQueryService.PlansAsync))
            {
                if (!Lookups.Task.IsCompleted) Overlapped = true;
                PlanCalls++;
                return Task.FromResult(new PagedResult<ProductionPlanListItem>([], 0));
            }
            throw new NotSupportedException();
        }
    }
}
