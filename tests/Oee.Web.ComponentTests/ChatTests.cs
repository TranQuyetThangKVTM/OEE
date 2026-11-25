using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using MudBlazor.Services;
using Oee.Application.Chat;
using Oee.Application.Common;
using Oee.Web.Chat;
using Oee.Web.Components.Chat;

namespace Oee.Web.ComponentTests;

public sealed class ChatTests : BunitContext, IAsyncLifetime
{
    private readonly Recorder _recorder = new();

    public ChatTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddMudServices();
        Services.AddAuthorizationCore();
        Services.AddScoped<ChatRequestRunner>();
        Services.AddScoped(_ => new Stamp());
        Services.AddScoped<IUseCaseExecutor>(provider => new Executor(_recorder, provider.GetRequiredService<Stamp>()));
    }

    Task IAsyncLifetime.InitializeAsync() => Task.CompletedTask;
    async Task IAsyncLifetime.DisposeAsync() => await base.DisposeAsync();

    [Fact]
    public async Task AnonymousUserHasNoLauncherAndCannotCallRunner()
    {
        this.AddAuthorization().SetNotAuthorized();
        var launcher = Render<ChatLauncher>();
        Assert.Empty(launcher.FindAll("button"));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => Services.GetRequiredService<ChatRequestRunner>().AskAsync("Trợ giúp", default));
        Assert.Empty(_recorder.Scopes);
    }

    [Fact]
    public void HeaderButtonTogglesPanelWithoutLeavingAFloatingLauncher()
    {
        this.AddAuthorization().SetAuthorized("reader");
        Render<MudPopoverProvider>();
        var launcher = Render<ChatLauncher>();

        Assert.Empty(launcher.FindAll(".mud-fab, .mud-fab-menu-container, .oee-chat-host"));
        launcher.Find("button[aria-label='Mở trợ lý hỏi đáp OEE']").Click();
        Assert.Single(launcher.FindAll(".oee-chat-host [role='region']"));
        Assert.Equal("true", launcher.Find("button[aria-label='Mở trợ lý hỏi đáp OEE']").GetAttribute("aria-expanded"));

        launcher.Find("button[aria-label='Mở trợ lý hỏi đáp OEE']").Click();
        Assert.Empty(launcher.FindAll(".oee-chat-host"));
        Assert.Equal("false", launcher.Find("button[aria-label='Mở trợ lý hỏi đáp OEE']").GetAttribute("aria-expanded"));
    }

    [Fact]
    public async Task EachQuestionRunsInItsOwnScope()
    {
        this.AddAuthorization().SetAuthorized("reader");
        var rootStamp = Services.GetRequiredService<Stamp>().Id;
        var runner = Services.GetRequiredService<ChatRequestRunner>();
        await runner.AskAsync("Trợ giúp", default);
        await runner.AskAsync("OEE hôm nay", default);
        Assert.Equal(2, _recorder.Scopes.Distinct().Count());
        Assert.DoesNotContain(rootStamp, _recorder.Scopes);
    }

    [Fact]
    public void PanelSupportsSafeFormattedAnswersAndClearWithoutOverlayOrExternalLinks()
    {
        this.AddAuthorization().SetAuthorized("reader");
        Render<MudPopoverProvider>();
        var launcher = Render<ChatLauncher>();
        launcher.Find("button").Click();
        launcher.Find("input").Input("OEE hôm nay");
        launcher.Find("button[aria-label='Gửi câu hỏi']").Click();
        launcher.WaitForAssertion(() => Assert.Contains("untrusted-answer", launcher.Markup, StringComparison.Ordinal));
        Assert.Empty(launcher.FindAll("script"));
        Assert.Empty(launcher.FindAll("img"));
        Assert.Empty(launcher.FindAll("a[href^='javascript:']"));
        Assert.Empty(launcher.FindAll("a[href^='//']"));
        Assert.Empty(launcher.FindAll(".mud-overlay, .mud-dialog"));
        Assert.Single(launcher.FindAll("a[href='/live-oee']"));
        Assert.Contains(launcher.FindAll("strong"), element => element.TextContent == "83,4%");
        launcher.Find("button[aria-label='Xóa hội thoại']").Click();
        launcher.WaitForAssertion(() => Assert.DoesNotContain("untrusted-answer", launcher.Markup, StringComparison.Ordinal));
    }

    [Fact]
    public void EnterSendsAndHistorySurvivesExpandCloseAndReopen()
    {
        this.AddAuthorization().SetAuthorized("reader");
        Render<MudPopoverProvider>();
        var launcher = Render<ChatLauncher>();
        launcher.Find("button").Click();
        launcher.Find("input").Input("OEE hôm nay");
        launcher.Find("input").KeyDown(new KeyboardEventArgs { Key = "Enter" });
        launcher.WaitForAssertion(() => Assert.Contains("untrusted-answer", launcher.Markup, StringComparison.Ordinal));
        launcher.Find("button[aria-label='Mở rộng trò chuyện']").Click();
        Assert.Single(launcher.FindAll("button[aria-label='Thu nhỏ trò chuyện']"));
        Assert.Contains("untrusted-answer", launcher.Markup, StringComparison.Ordinal);
        launcher.Find("button[aria-label='Đóng trợ lý OEE']").Click();
        Assert.Empty(launcher.FindAll("[role='region']"));
        launcher.Find("button[aria-label='Mở trợ lý hỏi đáp OEE']").Click();
        Assert.Contains("untrusted-answer", launcher.Markup, StringComparison.Ordinal);
        Assert.Single(_recorder.Scopes);
    }

    [Fact]
    public async Task ClosingPanelCancelsPendingQuestionAndPreventsLateReply()
    {
        this.AddAuthorization().SetAuthorized("reader");
        _recorder.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Render<MudPopoverProvider>();
        var launcher = Render<ChatLauncher>();
        launcher.Find("button").Click();
        launcher.Find("input").Input("OEE hôm nay");
        var sending = launcher.Find("button[aria-label='Gửi câu hỏi']").ClickAsync(new());
        launcher.WaitForAssertion(() => Assert.Single(_recorder.Tokens));
        Assert.True(launcher.Find("button[aria-label='Gửi câu hỏi']").HasAttribute("disabled"));
        launcher.Find("button[aria-label='Đóng trợ lý OEE']").Click();
        await sending;
        Assert.True(_recorder.Tokens[0].IsCancellationRequested);
        _recorder.Pending.SetResult(Answer());
        launcher.Find("button[aria-label='Mở trợ lý hỏi đáp OEE']").Click();
        Assert.DoesNotContain("untrusted-answer", launcher.Markup, StringComparison.Ordinal);
    }

    private static ChatAnswerDto Answer() => new("**Tóm tắt hôm nay**\nOEE **83,4%**\n<img src=x onerror=alert(1)> untrusted-answer",
        [new("Live", "/live-oee"), new("Unsafe", "javascript:alert(1)"), new("External", "//example.test")], ["Plan nào đang chạy?"], ChatIntentKind.OeeSummary);
    private sealed class Recorder
    {
        public List<Guid> Scopes { get; } = [];
        public List<CancellationToken> Tokens { get; } = [];
        public TaskCompletionSource<ChatAnswerDto>? Pending { get; set; }
    }
    private sealed class Stamp { public Guid Id { get; } = Guid.NewGuid(); }
    private sealed class Executor(Recorder recorder, Stamp stamp) : IUseCaseExecutor
    {
        public Task<TResult> ExecuteAsync<TResult>(IRequest<TResult> request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            recorder.Scopes.Add(stamp.Id);
            recorder.Tokens.Add(cancellationToken);
            return recorder.Pending is null ? Task.FromResult((TResult)(object)Answer()) : WaitAsync<TResult>(recorder.Pending.Task, cancellationToken);
        }
        private static async Task<TResult> WaitAsync<TResult>(Task<ChatAnswerDto> answer, CancellationToken ct) => (TResult)(object)await answer.WaitAsync(ct);
        public Task ExecuteAsync(ICommand command, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
