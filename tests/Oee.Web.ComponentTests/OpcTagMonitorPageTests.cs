using Bunit;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Oee.Application.Common;
using Oee.Application.PlcIntegration.Monitoring;
using Oee.Domain.Common;
using Oee.Web.Components.Pages.Plc;
using Oee.Web.Security;
using Oee.Web.Components.Shared;

namespace Oee.Web.ComponentTests;

public sealed class OpcTagMonitorPageTests : BunitContext, IAsyncLifetime
{
    private readonly MonitorRequests _requests = new();
    public OpcTagMonitorPageTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        var auth = this.AddAuthorization();
        auth.SetAuthorized("viewer");
        auth.SetRoles("Viewer");
        Services.AddScoped<ScopedPrincipalAccessor>();
        Services.AddScoped<OpcTagMonitorRequests>();
        Services.AddSingleton<IUseCaseExecutor>(_requests);
        Render<MudPopoverProvider>();
    }
    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;
    async Task IAsyncLifetime.DisposeAsync() => await base.DisposeAsync();

    [Fact]
    public async Task DeviceGroupsStayCollapsedWhenMonitorRefreshes()
    {
        var page = Render<OpcTagMonitor>();
        page.WaitForAssertion(() => Assert.Equal(4, page.FindAll("tbody tr").Count));
        var selector = page.FindComponent<TableGroupingSelector>();
        Assert.False(selector.Instance.AllowProduct);
        await page.InvokeAsync(() => selector.Instance.ValueChanged.InvokeAsync(TableGroupingMode.Machine));
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll(".oee-table-group").Count));
        page.Find("button[aria-label='Thu gọn tất cả nhóm']").Click();
        page.WaitForAssertion(() => Assert.DoesNotContain(page.FindAll("tbody button"), b => b.TextContent.Contains("Đọc trực tiếp", StringComparison.Ordinal)));
        page.FindAll("button").Single(b => b.TextContent.Contains("Tải lại ánh xạ", StringComparison.Ordinal)).Click();
        page.WaitForAssertion(() => Assert.True(_requests.ListRequests >= 2));
        Assert.DoesNotContain(page.FindAll("tbody button"), b => b.TextContent.Contains("Đọc trực tiếp", StringComparison.Ordinal));
        page.Find("button[aria-label='Mở tất cả nhóm']").Click();
        page.WaitForAssertion(() => Assert.Equal(4, page.FindAll("tbody button").Count(b => b.TextContent.Contains("Đọc trực tiếp", StringComparison.Ordinal))));
    }

    [Fact]
    public async Task FiltersIncludeFaultyInputsButExcludeUnsubscribedOutputsAndDirectReadStaysSeparate()
    {
        var page = Render<OpcTagMonitor>();
        page.WaitForAssertion(() => Assert.Equal(4, page.FindAll("tbody tr").Count));
        Assert.Contains("Good", page.Markup, StringComparison.Ordinal);
        Assert.Contains("Mất kết nối", page.Find("table").TextContent, StringComparison.Ordinal);
        Assert.Contains("Đầu ra · đọc trực tiếp", page.Markup, StringComparison.Ordinal);
        Assert.Contains("opc-tag-row--changed", page.Markup, StringComparison.Ordinal);

        await page.InvokeAsync(() => page.FindComponent<MudCheckBox<bool>>().Instance.ValueChanged.InvokeAsync(true));
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("tbody tr").Count));
        Assert.DoesNotContain("ns=2;s=Output", page.Markup, StringComparison.Ordinal);

        await page.InvokeAsync(() => page.FindComponent<MudCheckBox<bool>>().Instance.ValueChanged.InvokeAsync(false));
        var search = page.FindComponent<MudTextField<string>>();
        await page.InvokeAsync(() => search.Instance.ValueChanged.InvokeAsync("ns=2;s=Total"));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("tbody tr")));
        page.FindAll("button").Single(button => button.TextContent.Trim() == "Đọc trực tiếp").Click();
        page.WaitForAssertion(() => Assert.Contains("999", page.Markup, StringComparison.Ordinal));
        Assert.Equal(1, _requests.DirectReads);
        Assert.Equal("10", Assert.Single(_requests.Snapshot.Values, value => value.Role == TagRoleEnum.TotalCounter).Value);
        Assert.Equal(1, _requests.ListRequests);
    }

    [Fact]
    public async Task PauseFreezesScreenThenResumeRefreshesWithoutReloadingMappings()
    {
        var page = Render<OpcTagMonitor>();
        page.WaitForAssertion(() => Assert.Equal(4, page.FindAll("tbody tr").Count));
        page.FindAll("button").Single(button => button.TextContent.Trim() == "Tạm dừng màn hình").Click();
        var before = _requests.SnapshotRequests;
        await Task.Delay(2200);
        Assert.Equal(before, _requests.SnapshotRequests);
        _requests.Snapshot = _requests.Snapshot with { Values = _requests.Snapshot.Values.Select(value => value with { Value = "777" }).ToArray() };
        page.FindAll("button").Single(button => button.TextContent.Trim() == "Tiếp tục màn hình").Click();
        page.WaitForAssertion(() => Assert.Contains("777", page.Markup, StringComparison.Ordinal));
        Assert.Equal(1, _requests.ListRequests);
    }

    [Fact]
    public async Task DeviceFilterReloadAndDisposalDoNotLeaveTimerRunning()
    {
        var page = Render<OpcTagMonitor>();
        page.WaitForAssertion(() => Assert.Equal(4, page.FindAll("tbody tr").Count));
        await page.InvokeAsync(() => page.FindComponent<MudSelect<Guid?>>().Instance.ValueChanged.InvokeAsync(_requests.OtherDevice));
        page.WaitForAssertion(() => Assert.Single(page.FindAll("tbody tr")));
        page.FindAll("button").Single(button => button.TextContent.Trim() == "Tải lại ánh xạ").Click();
        page.WaitForAssertion(() => Assert.Equal(2, _requests.ListRequests));
        await page.Instance.DisposeAsync();
        var before = _requests.SnapshotRequests;
        await Task.Delay(2200);
        Assert.Equal(before, _requests.SnapshotRequests);
    }

    [Fact]
    public void FullscreenUsesSameMonitorWithoutRecursivelyOfferingAnotherScreen()
    {
        var provider = Render<MudDialogProvider>();
        var page = Render<OpcTagMonitor>();
        page.FindAll("button").Single(button => button.TextContent.Trim() == "Xem màn hình").Click();
        provider.WaitForAssertion(() =>
        {
            Assert.Single(provider.FindComponents<OpcTagMonitorScreenDialog>());
            Assert.True(provider.FindComponent<OpcTagMonitor>().Instance.ScreenMode);
            Assert.DoesNotContain(provider.FindAll("button"), button => button.TextContent.Trim() == "Xem màn hình");
            var pageTableId = page.FindComponent<TableViewTools>().Instance.TableId;
            var screenTableId = provider.FindComponent<TableViewTools>().Instance.TableId;
            Assert.NotEqual(pageTableId, screenTableId);
            Assert.NotNull(page.Find($"#{pageTableId} table"));
            Assert.NotNull(provider.Find($"#{screenTableId} table"));
        });
    }

    [Fact]
    public void TagFreshnessUsesReceptionAndQualityInsteadOfValueAgeOrFrameHealth()
    {
        var mapping = new OpcTagMonitorMapping(Guid.NewGuid(), Guid.NewGuid(), "M01", "Machine", "OPC", "Total",
            TagRoleEnum.TotalCounter, TagDirectionEnum.Read, "Double", true);
        var now = DateTimeOffset.UtcNow;
        var value = new OpcTagObservation(mapping.DeviceId, mapping.NodeId, mapping.Role, "100", "Double", true, "Good",
            now.AddDays(-10), now, now.AddDays(-10), true);
        var fresh = new OpcTagMonitorRow(mapping, value, "NoData", now, 30);
        Assert.False(fresh.Problem);
        Assert.False(fresh.Changed);
        Assert.Equal("Good", fresh.Status);
        Assert.True((fresh with { At = now.AddSeconds(31) }).Problem);
        Assert.True((fresh with { Connection = "Disconnected" }).Problem);
        Assert.True((fresh with { Observation = value with { IsGood = false } }).Problem);
    }

    private sealed class MonitorRequests : IUseCaseExecutor
    {
        public readonly Guid Device = Guid.NewGuid();
        public readonly Guid OtherDevice = Guid.NewGuid();
        public readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
        public int ListRequests;
        public int SnapshotRequests;
        public int DirectReads;
        public OpcTagMonitorSnapshot Snapshot;
        private readonly IReadOnlyCollection<OpcTagMonitorMapping> _mappings;
        public MonitorRequests()
        {
            _mappings = [
                new(Guid.NewGuid(), Device, "M01", "Machine 1", "OPC", "ns=2;s=Total", TagRoleEnum.TotalCounter, TagDirectionEnum.Read, "Double", true),
                new(Guid.NewGuid(), Device, "M01", "Machine 1", "OPC", "ns=2;s=Running", TagRoleEnum.Running, TagDirectionEnum.Read, "Boolean", true),
                new(Guid.NewGuid(), Device, "M01", "Machine 1", "OPC", "ns=2;s=Output", TagRoleEnum.Oee, TagDirectionEnum.Write, "Double", true),
                new(Guid.NewGuid(), OtherDevice, "M02", "Machine 2", "OPC", "ns=2;s=OtherTotal", TagRoleEnum.TotalCounter, TagDirectionEnum.Read, "Double", true)
            ];
            Snapshot = new(Now, 30, [
                new(Device, "ns=2;s=Total", TagRoleEnum.TotalCounter, "10", "Double", true, "Good", Now.AddDays(-3), Now, Now, true),
                new(Device, "ns=2;s=Running", TagRoleEnum.Running, "—", "Boolean", false, "BadNoCommunication", Now, Now, null, false)
            ], new Dictionary<Guid, string> { [Device] = "Connected", [OtherDevice] = "Disconnected" });
        }
        public Task<T> ExecuteAsync<T>(IRequest<T> request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            object result = request switch
            {
                ListOpcTagMonitorMappingsQuery => List(),
                GetOpcTagMonitorSnapshotQuery => ReadSnapshot(),
                ReadOpcTagDirectQuery read => Direct(read),
                _ => throw new NotSupportedException()
            };
            return Task.FromResult((T)result);
        }
        private IReadOnlyCollection<OpcTagMonitorMapping> List() { ListRequests++; return _mappings; }
        private OpcTagMonitorSnapshot ReadSnapshot() { SnapshotRequests++; return Snapshot; }
        private OpcTagDirectRead Direct(ReadOpcTagDirectQuery request)
        { DirectReads++; return new(request.ExpectedNodeId, "999", "Double", true, "Good", Now, Now); }
        public Task ExecuteAsync(ICommand command, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Monitor is read only");
    }
}
