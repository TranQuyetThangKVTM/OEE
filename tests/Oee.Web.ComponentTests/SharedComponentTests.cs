using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;
using Oee.Application.PlcIntegration;
using Oee.Domain.Common;
using Oee.Web.Components.Shared;

namespace Oee.Web.ComponentTests;

/// <summary>bUnit Test cho các component dùng lại: KPI, PageHeader, PageTable và chỉ báo kết nối OPC.</summary>
public sealed class SharedComponentTests : BunitContext, IAsyncLifetime
{
    /// <summary>Đăng ký dịch vụ MudBlazor cần để render component độc lập.</summary>
    public SharedComponentTests() { JSInterop.Mode = JSRuntimeMode.Loose; Services.AddMudServices(); }
    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;
    async Task IAsyncLifetime.DisposeAsync() => await base.DisposeAsync();
    /// <summary>Kiểm tra MetricCard vừa hiển thị số vừa cung cấp aria-label cho công cụ hỗ trợ.</summary>
    [Fact] public void MetricCardExposesAccessibleLabelAndValue() { var component = Render<MetricCard>(p => p.Add(x => x.Label, "OEE tổng thể").Add(x => x.Value, "85.4%")); Assert.Contains("aria-label=\"OEE tổng thể\"", component.Markup, StringComparison.Ordinal); Assert.Contains("85.4%", component.Markup, StringComparison.Ordinal); Assert.Equal("true", component.Find(".metric-card__icon").GetAttribute("aria-hidden")); }
    /// <summary>Chỉ báo phân biệt kết nối, mất kết nối và chưa cấu hình, không dùng banner ngang.</summary>
    [Theory]
    [InlineData(ConnectivityStateEnum.Connected, "connected", "Đã kết nối")]
    [InlineData(ConnectivityStateEnum.Disconnected, "offline", "Mất kết nối")]
    [InlineData(ConnectivityStateEnum.NotConfigured, "unconfigured", "Chưa cấu hình")]
    public void OpcIndicatorShowsCurrentStateAndLinksToMonitor(ConnectivityStateEnum state, string kind, string label)
    {
        Services.AddScoped<IOpcUiStatusService>(_ => new FakeStatusService(state));
        var component = Render<OpcConnectionIndicator>(p => p.Add(x => x.RefreshInterval, TimeSpan.FromHours(1)));
        component.WaitForAssertion(() =>
        {
            var link = component.Find($"a.opc-indicator--{kind}");
            Assert.Equal("/plc/tag-monitor", link.GetAttribute("href"));
            Assert.Contains(label, link.TextContent, StringComparison.Ordinal);
            Assert.Contains("Kiểm tra lúc", link.GetAttribute("title"), StringComparison.Ordinal);
            Assert.Empty(component.FindAll(".mud-alert"));
        });
    }

    /// <summary>Lỗi truy vấn không được biến thành con số thiết bị mất kết nối giả.</summary>
    [Fact]
    public void OpcIndicatorReportsUnavailableStatusWithoutFabricatingDeviceCounts()
    {
        Services.AddScoped<IOpcUiStatusService>(_ => new FailedStatusService());
        var component = Render<OpcConnectionIndicator>();
        component.WaitForAssertion(() =>
        {
            var link = component.Find("a.opc-indicator--offline");
            Assert.Contains("Chưa đọc được trạng thái", link.TextContent, StringComparison.Ordinal);
            Assert.DoesNotContain("thiết bị", link.GetAttribute("title"), StringComparison.Ordinal);
            Assert.DoesNotContain("private-error", component.Markup, StringComparison.Ordinal);
        });
    }

    /// <summary>Nút Làm mới do PageHeader render phải gọi đúng callback tải lại của trang cha.</summary>
    [Fact]
    public void PageHeaderRefreshInvokesParentCallback()
    {
        int refreshCount = 0;
        RenderFragment metrics = builder =>
        {
            builder.OpenComponent<MetricCard>(0);
            builder.AddAttribute(1, nameof(MetricCard.Label), "Tổng kế hoạch");
            builder.AddAttribute(2, nameof(MetricCard.Value), refreshCount == 0 ? "33" : "0");
            builder.CloseComponent();
        };
        var component = Render<PageHeader>(parameters => parameters
            .Add(x => x.Title, "Trang kiểm thử")
            .Add(x => x.Metrics, metrics)
            .Add(x => x.OnRefresh, () => refreshCount++));

        Assert.Equal("Trang kiểm thử", component.Find("h1").TextContent);
        Assert.Equal("33", component.Find(".metric-card__value").TextContent);
        component.Find("button").Click();

        Assert.Equal(1, refreshCount);
        Assert.Contains("Làm mới", component.Markup, StringComparison.Ordinal);
    }

    /// <summary>Menu chỉ thực thi thao tác được chọn sau khi người dùng mở menu.</summary>
    [Fact]
    public void RowActionMenuRequiresOpeningAndRunsOnlyTheSelectedCommand()
    {
        var calls = 0;
        var popovers = Render<MudPopoverProvider>();
        RenderFragment items = builder =>
        {
            builder.OpenComponent<RowAction>(0);
            builder.AddAttribute(1, nameof(RowAction.Label), "Sửa bản ghi kiểm thử");
            builder.AddAttribute(2, nameof(RowAction.OnClick), EventCallback.Factory.Create<Microsoft.AspNetCore.Components.Web.MouseEventArgs>(this, () => calls++));
            builder.CloseComponent();
        };
        var component = Render<RowActionMenu>(parameters => parameters.Add(x => x.Label, "Thao tác kiểm thử").Add(x => x.ChildContent, items));

        Assert.Equal(0, calls);
        component.Find("button").Click();
        popovers.WaitForAssertion(() => Assert.NotEmpty(popovers.FindAll("[aria-label='Sửa bản ghi kiểm thử']")));
        popovers.Find("[aria-label='Sửa bản ghi kiểm thử']").Click();

        Assert.Equal(1, calls);
    }

    /// <summary>Request cũ bị hủy khi reload nhanh không xóa dữ liệu và request mới vẫn hoàn tất.</summary>
    [Fact]
    public async Task PageTableIgnoresCancellationFromSupersededRequest()
    {
        // Arrange: lần tải đầu trả dữ liệu cũ; lần kế tiếp chờ đến khi bị request mới hủy.
        int callCount = 0;
        int blockNextCall = 0;
        TaskCompletionSource blockedRequestStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<TableState, CancellationToken, Task<TableData<string>>> provider = async (_, cancellationToken) =>
        {
            int currentCall = Interlocked.Increment(ref callCount);
            if (Interlocked.Exchange(ref blockNextCall, 0) == 1)
            {
                blockedRequestStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            return new TableData<string>
            {
                Items = [currentCall == 1 ? "Dữ liệu cũ" : "Dữ liệu mới"],
                TotalItems = 1
            };
        };
        RenderFragment header = builder => builder.AddContent(0, "Giá trị");
        RenderFragment<string> row = item => builder => builder.AddContent(0, item);
        var component = Render<PageTable<string>>(parameters => parameters
            .Add(x => x.ItemsProvider, provider)
            .Add(x => x.HeaderContent, header)
            .Add(x => x.RowTemplate, row));
        component.WaitForAssertion(() => Assert.Contains("Dữ liệu cũ", component.Markup, StringComparison.Ordinal));

        // Act: reload lần hai hủy request đang chờ và bắt đầu request thay thế.
        Interlocked.Exchange(ref blockNextCall, 1);
        Task supersededReload = component.InvokeAsync(component.Instance.ReloadAsync);
        await blockedRequestStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task latestReload = component.InvokeAsync(component.Instance.ReloadAsync);
        await Task.WhenAll(supersededReload, latestReload).WaitAsync(TimeSpan.FromSeconds(5));

        // Assert: cancellation không trở thành empty/error state; kết quả mới nhất được hiển thị.
        component.WaitForAssertion(() => Assert.Contains("Dữ liệu mới", component.Markup, StringComparison.Ordinal));
        Assert.DoesNotContain("Không có dữ liệu", component.Markup, StringComparison.Ordinal);
    }

    private sealed class FakeStatusService(ConnectivityStateEnum state) : IOpcUiStatusService
    {
        public Task<OpcUiStatus> GetAsync(CancellationToken cancellationToken)
            => Task.FromResult(new OpcUiStatus(2, state == ConnectivityStateEnum.Connected ? 2 : 1,
                state == ConnectivityStateEnum.Connected ? 0 : 1, state, DateTimeOffset.UtcNow));
    }

    private sealed class FailedStatusService : IOpcUiStatusService
    {
        public Task<OpcUiStatus> GetAsync(CancellationToken cancellationToken)
            => Task.FromException<OpcUiStatus>(new InvalidOperationException("private-error"));
    }
}
