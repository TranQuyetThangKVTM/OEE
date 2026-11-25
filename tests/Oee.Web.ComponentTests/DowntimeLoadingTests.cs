using System.Reflection;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;
using Oee.Application.Contracts.Modules;
using Oee.Application.Modules;
using Oee.Web.Components.Pages.Production;

namespace Oee.Web.ComponentTests;

public sealed class DowntimeLoadingTests : BunitContext, IAsyncLifetime
{
    public DowntimeLoadingTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        this.AddAuthorization().SetAuthorized("viewer");
        Services.AddScenarioRequests();
    }
    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;
    async Task IAsyncLifetime.DisposeAsync() => await base.DisposeAsync();

    [Fact]
    public async Task TableWaitsForSlowLookupsAndStatisticsBeforeUsingTheScopedQueryService()
    {
        var query = DispatchProxy.Create<IProductionModuleQueryService, SlowQueries>();
        var proxy = (SlowQueries)(object)query;
        Services.AddSingleton(query);
        var page = Render<DowntimeEvents>();
        Assert.Empty(page.FindAll("table"));
        Assert.Equal(0, proxy.DowntimeCalls);

        await page.InvokeAsync(() => proxy.Lookups.SetResult(new ProductionLookups([], [], [], [], [])));
        page.WaitForAssertion(() =>
        {
            Assert.Single(page.FindAll("table"));
            Assert.Equal(4, proxy.DowntimeCalls); // Three statistics followed by the first table page.
        });
        Assert.False(proxy.Overlapped);
    }

    public class SlowQueries : DispatchProxy
    {
        public TaskCompletionSource<ProductionLookups> Lookups { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int DowntimeCalls { get; private set; }
        public bool Overlapped { get; private set; }
        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method?.Name == nameof(IProductionModuleQueryService.LookupsAsync)) return Lookups.Task;
            if (method?.Name == nameof(IProductionModuleQueryService.DowntimeAsync))
            {
                if (!Lookups.Task.IsCompleted) Overlapped = true;
                DowntimeCalls++;
                return Task.FromResult(new PagedResult<DowntimeListItem>([], 0));
            }
            throw new NotSupportedException();
        }
    }
}
