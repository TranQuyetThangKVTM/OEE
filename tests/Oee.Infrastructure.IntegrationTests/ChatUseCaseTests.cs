using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Oee.Application.Chat;
using Oee.Application.Common;
using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.Reporting;
using Oee.Application.Modules;
using Oee.Application.Oee;
using Oee.Application.Reporting;

namespace Oee.Infrastructure.IntegrationTests;

public sealed class ChatUseCaseTests
{
    private static readonly Guid UnitId = Guid.NewGuid();
    private static readonly Guid SiteId = Guid.NewGuid();
    private static readonly DateOnly Today = new(2026, 10, 26);

    [Theory]
    [InlineData("Tự động tạo kế hoạch ngày mai")]
    [InlineData("Tự xuất PDF theo ca")]
    public async Task AutomationQuestionsExplainPickerWithoutStartingJobsOrReadingFacts(string question)
    {
        using var provider = Services((_, _) => throw new InvalidOperationException("No data access from scheduling guidance")).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var answer = await scope.ServiceProvider.GetRequiredService<IUseCaseExecutor>().ExecuteAsync(new AskChatbotQuery(question));
        Assert.Equal(ChatIntentKind.HowTo, answer.ResolvedIntent);
        Assert.Contains("Lịch kế hoạch / PDF ca", answer.Text, StringComparison.Ordinal);
        Assert.Contains("bật và lưu", answer.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HelpDoesNotQueryDatabaseAndCancellationIsPreserved()
    {
        using var provider = Services((_, _) => throw new InvalidOperationException("Data access is forbidden for help")).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var executor = scope.ServiceProvider.GetRequiredService<IUseCaseExecutor>();
        var answer = await executor.ExecuteAsync(new AskChatbotQuery("Trợ giúp"));
        Assert.Equal(ChatIntentKind.Help, answer.ResolvedIntent);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.ExecuteAsync(new AskChatbotQuery("OEE hôm nay"), cancellation.Token));
    }

    [Fact]
    public async Task UsesSiteDateAndResolvesCanonicalMachineWithoutChangingCalculatedMetrics()
    {
        DashboardQueryRequest? captured = null;
        using var provider = Services((name, args) => name switch
        {
            "GetFilterOptionsAsync" => Task.FromResult(Filters()),
            "GetAsync" when args[0] is DashboardQueryRequest request => Capture(request),
            _ => throw new InvalidOperationException(name)
        }).BuildServiceProvider();
        Task<DashboardModel> Capture(DashboardQueryRequest request)
        {
            captured = request;
            return Task.FromResult(new DashboardModel(new(.7m, null, null, null), new(.9m, null, null, null),
                new(.8m, null, null, null), new(null, null, null, null), new(100, 98, 2, 200), new(60, 54, 43.2m), [], [], "Asia/Ho_Chi_Minh", true));
        }
        using var scope = provider.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IUseCaseExecutor>().ExecuteAsync(new AskChatbotQuery("OEE máy M01 hôm nay"));
        Assert.Equal(UnitId, captured!.WorkUnitId);
        Assert.Equal(Today, captured.FromDate);
        Assert.Contains(.7m.ToString("P1", CultureInfo.GetCultureInfo("vi-VN")), result.Text, StringComparison.Ordinal);
        Assert.Contains("chưa xác định", result.Text, StringComparison.Ordinal);
        Assert.Contains("Asia/Ho_Chi_Minh", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownMachineDoesNotFallBackToGlobalData()
    {
        using var provider = Services((name, _) => name == "GetFilterOptionsAsync" ? Task.FromResult(Filters()) : throw new InvalidOperationException("Unexpected global data query")).BuildServiceProvider();
        using var scope = provider.CreateScope();
        await Assert.ThrowsAsync<ArgumentException>(() => scope.ServiceProvider.GetRequiredService<IUseCaseExecutor>().ExecuteAsync(new AskChatbotQuery("OEE máy M999 hôm nay")));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DashboardSummaryUsesFullAlertCountsAndOnlyConfirmedLiveStates(bool hasProduction)
    {
        var now = new Clock().UtcNow;
        var live = new LiveOeeModel(
            [new(UnitId, "RUN", "Running", "Running", "Connected", null, null, null, null, 0, 0, 0, 0, null, null, null, now, now, null, null, false, 0),
             new(Guid.NewGuid(), "STOP", "Stopped", "Stopped", "Connected", null, null, null, null, 0, 0, 0, 0, null, null, null, now, now, null, null, false, 0),
             new(Guid.NewGuid(), "STALE", "Old state", "Running", "Connected", null, null, null, null, 0, 0, 0, 0, null, null, null, now, now, null, null, true, 600),
             new(Guid.NewGuid(), "OFFLINE", "Offline", "Running", "Disconnected", null, null, null, null, 0, 0, 0, 0, null, null, null, now, now, null, null, false, 0)],
            new(4, 4, 0, 4, 0, null, null, null, null, 0, 0, 0, 0, 0), null, null, null, 0, null, now, now);
        using var provider = Services((name, args) => name switch
        {
            "GetFilterOptionsAsync" => Task.FromResult(Filters()),
            "GetAsync" when args[0] is DashboardQueryRequest => Task.FromResult(new DashboardModel(
                new(.834m, null, null, null), new(1m, null, null, null), new(.836m, null, null, null), new(.998m, null, null, null),
                new(424907, 423869, 1038, 500000), new(60, 60, 50), [], [], "Asia/Ho_Chi_Minh", hasProduction)),
            "GetAsync" => Task.FromResult(live),
            "ListAsync" when args[0] is AlertListQuery { State: "Open", Severity: "Critical", PageSize: 1 }
                => Task.FromResult(new PagedResult<AlertListItem>([], 3)),
            "ListAsync" when args[0] is AlertListQuery { State: "Open", Severity: null, PageSize: 1 }
                => Task.FromResult(new PagedResult<AlertListItem>([], 1657)),
            _ => throw new InvalidOperationException(name)
        }).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var answer = await scope.ServiceProvider.GetRequiredService<IUseCaseExecutor>().ExecuteAsync(new AskChatbotQuery("Tóm tắt tình hình hôm nay"));
        Assert.Contains("1/4 đang chạy", answer.Text, StringComparison.Ordinal);
        Assert.Contains("2 máy chưa có dữ liệu", answer.Text, StringComparison.Ordinal);
        Assert.Contains("1657 cảnh báo đang mở", answer.Text, StringComparison.Ordinal);
        Assert.Contains("3 nghiêm trọng", answer.Text, StringComparison.Ordinal);
        Assert.Equal("/", Assert.Single(answer.Citations).Href);
        Assert.Equal(3, answer.FollowUpSuggestions.Count);
        if (hasProduction) Assert.Contains("423.869", answer.Text, StringComparison.Ordinal);
        else Assert.Contains("Chưa có snapshot", answer.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PlanAttainmentUsesFullTotalsAndDoesNotClampAtOneHundredPercent()
    {
        using var provider = Services((name, _) => name switch
        {
            "GetFilterOptionsAsync" => Task.FromResult(Filters()),
            "PlansAsync" => Task.FromResult(new ChatPlanSummary(200, 100, 150, [])),
            _ => throw new InvalidOperationException(name)
        }).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var answer = await scope.ServiceProvider.GetRequiredService<IUseCaseExecutor>().ExecuteAsync(new AskChatbotQuery("Đạt bao nhiêu % kế hoạch hôm nay?"));
        Assert.Contains(1.5m.ToString("P1", CultureInfo.GetCultureInfo("vi-VN")), answer.Text, StringComparison.Ordinal);
        Assert.Contains("200 kế hoạch", answer.Text, StringComparison.Ordinal);
        Assert.Equal("/production/plans", Assert.Single(answer.Citations).Href);
    }

    [Fact]
    public async Task CurrentShiftUsesMachineSiteAndIncludesOvernightShiftFromPreviousDay()
    {
        var otherSite = Guid.NewGuid();
        var lookups = new ProductionLookups(
            [new(UnitId, "M-001", "Machine", Detail: "Asia/Ho_Chi_Minh", SiteId: SiteId)], [], [],
            [new(Guid.NewGuid(), "S3", "Ca đêm", StartTime: new(22, 0), EndTime: new(6, 0), SiteId: SiteId),
             new(Guid.NewGuid(), "OTHER", "Other site", StartTime: new(22, 0), EndTime: new(6, 0), SiteId: otherSite)], []);
        using var provider = Services((name, _) => name switch
        {
            "GetFilterOptionsAsync" => Task.FromResult(Filters()),
            "LookupsAsync" => Task.FromResult(lookups),
            _ => throw new InvalidOperationException("Shift configuration must not require realtime PLC data")
        }).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IUseCaseExecutor>().ExecuteAsync(new AskChatbotQuery("Máy M01 ca hiện tại còn bao lâu nữa?"));
        Assert.Contains("S3", result.Text, StringComparison.Ordinal);
        Assert.Contains("300", result.Text, StringComparison.Ordinal);
        Assert.Contains("26/10 06:00 +07:00", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("OTHER", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LongDowntimeDoesNotClaimOfflineOrFutureStateIsAConfirmedStop()
    {
        var now = new Clock().UtcNow;
        var live = new LiveOeeModel(
            [new(UnitId, "OFFLINE", "Offline", "Stopped", "Disconnected", null, null, null, null, 0, 0, 0, 0, null, null, null, now, now, now.AddHours(-2), null, false, 0),
             new(Guid.NewGuid(), "REAL", "Confirmed", "Stopped", "Connected", null, null, null, null, 0, 0, 0, 0, null, null, null, now, now, now.AddMinutes(-30), null, false, 0),
             new(Guid.NewGuid(), "FUTURE", "Invalid time", "Stopped", "Connected", null, null, null, null, 0, 0, 0, 0, null, null, null, now, now, now.AddHours(1), null, false, 0)],
            new(3, 0, 2, 2, 1, null, null, null, null, 0, 0, 0, 0, 0), null, null, null, 0, null, now, now);
        using var provider = Services((name, _) => name switch
        {
            "GetFilterOptionsAsync" => Task.FromResult(Filters()),
            "GetAsync" => Task.FromResult(live),
            _ => throw new InvalidOperationException(name)
        }).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<IUseCaseExecutor>().ExecuteAsync(new AskChatbotQuery("Có máy nào đang dừng quá lâu không?"));
        Assert.Contains("REAL", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("OFFLINE", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("FUTURE", result.Text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("OEE tuần này so với tuần trước?", 10, 19, 10, 25)]
    [InlineData("OEE tháng này so với tháng trước?", 9, 1, 9, 30)]
    public async Task CalendarComparisonQueriesTheExplicitPreviousPeriod(string question, int fromMonth, int fromDay, int toMonth, int toDay)
    {
        var requests = new List<DashboardQueryRequest>();
        using var provider = Services((name, args) => name switch
        {
            "GetFilterOptionsAsync" => Task.FromResult(Filters()),
            "GetAsync" when args[0] is DashboardQueryRequest request => Capture(request),
            _ => throw new InvalidOperationException(name)
        }).BuildServiceProvider();
        Task<DashboardModel> Capture(DashboardQueryRequest request)
        {
            requests.Add(request);
            return Task.FromResult(new DashboardModel(new(.8m, null, null, null), new(.9m, null, null, null),
                new(.9m, null, null, null), new(.99m, null, null, null), new(100, 99, 1, 100), new(60, 54, 48), [], [], "Asia/Ho_Chi_Minh", true));
        }
        using var scope = provider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IUseCaseExecutor>().ExecuteAsync(new AskChatbotQuery(question));
        Assert.Equal(2, requests.Count);
        Assert.Equal(new DateOnly(2026, fromMonth, fromDay), requests[1].FromDate);
        Assert.Equal(new DateOnly(2026, toMonth, toDay), requests[1].ToDate);
    }

    private static ReportFilterOptions Filters() => new(Today,
        [new(SiteId, "SITE", "Site", Detail: "Asia/Ho_Chi_Minh")], [], [],
        [new(UnitId, "M-001", "Machine", Detail: "Asia/Ho_Chi_Minh")], [], [], []);

    [Fact]
    public async Task SpeedStandardAnswerPreservesFractionalSpeed()
    {
        using var provider = Services((name, _) => name switch
        {
            "GetFilterOptionsAsync" => Task.FromResult(Filters()),
            "CatalogAsync" => Task.FromResult(new ChatCatalogPage([new("M01", "SKU01", "sp/phút", 12.5m)], 1)),
            _ => throw new InvalidOperationException(name)
        }).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var answer = await scope.ServiceProvider.GetRequiredService<IUseCaseExecutor>().ExecuteAsync(new AskChatbotQuery("Liệt kê định mức tốc độ"));
        Assert.Contains("12,5", answer.Text, StringComparison.Ordinal);
        Assert.Equal("/master/work-unit-standards", Assert.Single(answer.Citations).Href);
    }

    private static IServiceCollection Services(Func<string, object?[], object?> query)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddOeeInfrastructure("");
        services.AddSingleton<IClock>(new Clock());
        services.AddScoped(_ => Stub<IReportAnalysisQueryService>(query));
        services.AddScoped(_ => Stub<IDashboardQueryService>(query));
        services.AddScoped(_ => Stub<ILiveOeeQueryService>(query));
        services.AddScoped(_ => Stub<IProductionModuleQueryService>(query));
        services.AddScoped(_ => Stub<IAlertReadStore>(query));
        services.AddScoped(_ => Stub<IChatReadStore>(query));
        return services;
    }

    private static T Stub<T>(Func<string, object?[], object?> query) where T : class
    {
        var value = DispatchProxy.Create<T, PortProxy>();
        ((PortProxy)(object)value).Query = query;
        return value;
    }

    public class PortProxy : DispatchProxy
    {
        public Func<string, object?[], object?> Query { get; set; } = default!;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => Query(targetMethod!.Name, args ?? []);
    }

    private sealed class Clock : IClock { public DateTimeOffset UtcNow => new(2026, 10, 25, 18, 0, 0, TimeSpan.Zero); }
}
