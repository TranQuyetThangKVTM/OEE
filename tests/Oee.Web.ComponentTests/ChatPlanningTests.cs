using System.Reflection;
using System.Security.Claims;
using Bunit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Oee.Application.Common;
using Oee.Application.Contracts.Modules;
using Oee.Application.Modules;
using Oee.Application.Production.Automation;
using Oee.Application.Security;
using Oee.Web.Chat;
using Oee.Web.Components.Chat;
using Oee.Web.Security;

namespace Oee.Web.ComponentTests;

public sealed class ChatPlanningTests : BunitContext, IAsyncLifetime
{
    private readonly Probe _probe = new();
    public ChatPlanningTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddAuthorizationCore();
        Services.AddHttpContextAccessor();
        Services.AddScoped<ScopedPrincipalAccessor>();
        Services.AddScoped<ICapabilityChecker, HttpCapabilityChecker>();
        Services.AddScoped<ICurrentActor, HttpCurrentActor>();
        Services.AddScoped<ChatRequestRunner>();
        Services.AddSingleton(_probe);
        Services.AddScoped<IUseCaseExecutor, Executor>();
        Services.AddScoped<IProductionModuleQueryService>(_ =>
        {
            var query = DispatchProxy.Create<IProductionModuleQueryService, QueryProxy>();
            ((QueryProxy)(object)query).Sample = _probe.Sample;
            return query;
        });
    }
    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;
    async Task IAsyncLifetime.DisposeAsync() => await base.DisposeAsync();

    [Fact]
    public async Task ViewerCannotManageScheduleEvenIfHttpContextHappensToBeAdmin()
    {
        var auth = this.AddAuthorization();
        auth.SetAuthorized("viewer");
        auth.SetRoles(RoleCapabilityMatrix.Viewer);
        Services.GetRequiredService<IHttpContextAccessor>().HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, RoleCapabilityMatrix.Admin)], "test"))
        };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Services.GetRequiredService<ChatRequestRunner>()
            .SaveScheduleAsync(new(_probe.Sample.Id, new(16, 0), true, null), default));
        Assert.Null(_probe.LastSave);
    }

    [Fact]
    public async Task SeparateScopeUsesAuthenticatedCircuitPrincipalForPermissionAndAudit()
    {
        var auth = this.AddAuthorization();
        auth.SetAuthorized("planner");
        auth.SetRoles(RoleCapabilityMatrix.PlanningManager);
        await Services.GetRequiredService<ChatRequestRunner>().SaveScheduleAsync(
            new(_probe.Sample.Id, new(16, 0), true, null), default);
        Assert.Equal("planner", _probe.Actor);
        Assert.True(_probe.Capability);
        Assert.Null(Services.GetRequiredService<ScopedPrincipalAccessor>().User);
    }

    [Fact]
    public async Task DialogRequiresSelectionAndSavesSelectedTemplateTimeAndIndependentPdfSwitch()
    {
        var auth = this.AddAuthorization();
        auth.SetAuthorized("planner");
        auth.SetRoles(RoleCapabilityMatrix.PlanningManager);
        Render<MudPopoverProvider>();
        var provider = Render<MudDialogProvider>();
        await Services.GetRequiredService<IDialogService>().ShowAsync<ProductionPlanScheduleDialog>("Schedule");
        provider.WaitForAssertion(() => Assert.Single(provider.FindComponents<MudTimePicker>()));
        provider.FindAll("button").Single(button => button.TextContent.Trim() == "Lưu lịch").Click();
        provider.WaitForAssertion(() => Assert.Contains("Hãy chọn kế hoạch mẫu", provider.Markup, StringComparison.Ordinal));
        Assert.Null(_probe.LastSave);
        var sample = provider.FindComponent<MudAutocomplete<SamplePlanListItem>>();
        await provider.InvokeAsync(() => sample.Instance.ValueChanged.InvokeAsync(_probe.Sample));
        var time = provider.FindComponent<MudTimePicker>();
        await provider.InvokeAsync(() => time.Instance.TimeChanged.InvokeAsync(TimeSpan.FromHours(17.5)));
        var switches = provider.FindComponents<MudSwitch<bool>>();
        await provider.InvokeAsync(() => switches.Single(item => item.Instance.Label == "Bật lịch tự động").Instance.ValueChanged.InvokeAsync(true));
        await provider.InvokeAsync(() => switches.Single(item => item.Instance.Label == "Tự xuất PDF OEE sau mỗi ca").Instance.ValueChanged.InvokeAsync(true));
        provider.FindAll("button").Single(button => button.TextContent.Trim() == "Lưu lịch").Click();
        provider.WaitForAssertion(() => Assert.NotNull(_probe.LastSave));
        Assert.Equal(_probe.Sample.Id, _probe.LastSave!.SamplePlanId);
        Assert.Equal(new TimeOnly(17, 30), _probe.LastSave.RunAt);
        Assert.True(_probe.LastSave.Enabled);
        Assert.True(_probe.LastSave.ExportShiftPdf);
        Assert.Equal(5, _probe.LastSave.PdfDelayMinutes);
    }

    [Fact]
    public void AutomationButtonIsHiddenWithoutPlanningPolicy()
    {
        var auth = this.AddAuthorization();
        auth.SetAuthorized("viewer");
        auth.SetPolicies("unrelated-policy");
        var panel = Render<ChatPanel>();
        Assert.DoesNotContain("Lịch kế hoạch / PDF ca", panel.Markup, StringComparison.Ordinal);
    }

    private sealed class Probe
    {
        public SamplePlanListItem Sample { get; } = new(Guid.NewGuid(), "KHM-0001", "Daily", null, 33, 11, 3, 1000,
            DateTimeOffset.UtcNow, "AQ==");
        public SaveProductionPlanScheduleCommand? LastSave { get; set; }
        public string? Actor { get; set; }
        public bool Capability { get; set; }
    }
    private sealed class Executor(Probe probe, ICapabilityChecker capabilities, ICurrentActor actor) : IUseCaseExecutor
    {
        public Task<T> ExecuteAsync<T>(IRequest<T> request, CancellationToken cancellationToken = default)
        {
            if (request is GetProductionPlanScheduleQuery) return Task.FromResult(default(T)!);
            if (request is SaveProductionPlanScheduleCommand save)
            {
                probe.LastSave = save;
                probe.Actor = actor.UserName;
                probe.Capability = capabilities.HasCapability(Capabilities.ProductionPlanManage);
                var result = new ProductionPlanSchedule(Guid.NewGuid(), save.Enabled, save.SamplePlanId, probe.Sample.Code,
                    save.RunAt, "Asia/Ho_Chi_Minh", new(2026, 10, 27), null, actor.UserName, DateTimeOffset.UtcNow,
                    PdfEnabled: save.ExportShiftPdf);
                return Task.FromResult((T)(object)result);
            }
            throw new InvalidOperationException();
        }
        public Task ExecuteAsync(ICommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
    public class QueryProxy : DispatchProxy
    {
        public SamplePlanListItem Sample { get; set; } = default!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name == "SamplesAsync"
            ? Task.FromResult(new PagedResult<SamplePlanListItem>([Sample], 1)) : throw new InvalidOperationException();
    }
}
