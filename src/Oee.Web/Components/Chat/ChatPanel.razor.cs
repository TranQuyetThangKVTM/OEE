using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using Oee.Application.Production.Automation;

namespace Oee.Web.Components.Chat;

public partial class ChatPanel
{
    [Parameter] public List<ChatMessage> Messages { get; set; } = [];
    [Parameter] public string UserName { get; set; } = string.Empty;
    [Parameter] public bool Expanded { get; set; }
    [Parameter] public EventCallback<bool> ExpandedChanged { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    private readonly CancellationTokenSource _lifetime = new();
    private readonly string _scrollId = $"oee-chat-{Guid.NewGuid():N}";
    private MudTextField<string>? _questionField;
    private string _input = string.Empty;
    private bool _busy;
    private bool _disposed;
    private bool _scrollPending = true;
    private bool _focusPending = true;
    private const string WelcomeText = "Xin chào! Mình là **trợ lý OEE**. Hỏi bất kỳ điều gì về tình hình sản xuất.\nMột số ví dụ:";
    private string PanelWidth => Expanded ? "min(720px, calc(100vw - 32px))" : "min(400px, calc(100vw - 32px))";
    private string PanelHeight => Expanded
        ? "calc(100dvh - var(--mud-appbar-height, 64px) - 24px)"
        : "min(640px, calc(100dvh - var(--mud-appbar-height, 64px) - 24px))";
    private string AnswerWidth => Expanded ? "min(560px, calc(100% - 48px))" : "min(288px, calc(100% - 48px))";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed) return;
        if (_scrollPending)
        {
            _scrollPending = false;
            await ScrollManager.ScrollToBottomAsync($"#{_scrollId}", ScrollBehavior.Auto);
        }
        if (_focusPending && !_busy && _questionField is not null)
        {
            _focusPending = false;
            await _questionField.FocusAsync();
        }
    }

    private Task ToggleExpandedAsync() => ExpandedChanged.InvokeAsync(!Expanded);
    private async Task OpenScheduleAsync()
    {
        var dialog = await Dialogs.ShowAsync<ProductionPlanScheduleDialog>("Tự động tạo kế hoạch",
            new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true });
        var result = await dialog.Result;
        if (!_disposed && result is { Canceled: false, Data: ProductionPlanSchedule saved })
        {
            var message = saved.Enabled
                ? $"Đã bật lịch: mỗi ngày lúc **{saved.RunAt:HH:mm}** ({saved.TimeZoneId}), tạo kế hoạch **ngày mai** từ mẫu **{saved.SamplePlanCode}**. Kế hoạch trùng được bỏ qua."
                : "Tự động tạo kế hoạch đang tắt. Các kế hoạch đã tạo được giữ nguyên.";
            message += saved.PdfEnabled ? $"\n**PDF theo ca:** tự xuất sau khi hết ca {saved.PdfDelayMinutes} phút. Mở lại lịch để tải báo cáo."
                : "\nTự xuất PDF theo ca đang tắt.";
            Messages.Add(new(false, message));
            _scrollPending = true;
        }
    }
    private Task HandlePanelKeyDownAsync(KeyboardEventArgs args) => args.Key == "Escape" ? OnClose.InvokeAsync() : Task.CompletedTask;
    private Task HandleInputKeyDownAsync(KeyboardEventArgs args) => args.Key == "Enter" && !args.ShiftKey && !args.IsComposing
        ? SendAsync(_input) : Task.CompletedTask;

    private async Task SendAsync(string question)
    {
        if (_busy || string.IsNullOrWhiteSpace(question) || question.Length > 1000) return;
        _busy = true;
        _input = string.Empty;
        Messages.Add(new(true, question.Trim()));
        _scrollPending = true;
        using var request = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        request.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            var answer = await Runner.AskAsync(question.Trim(), request.Token);
            if (!_disposed) Messages.Add(new(false, answer.Text, answer));
        }
        catch (OperationCanceledException) when (!_disposed)
        {
            Messages.Add(new(false, "Truy vấn mất quá nhiều thời gian. Hãy chọn khoảng ngày ngắn hơn rồi thử lại."));
        }
        catch (OperationCanceledException) when (_disposed) { }
        catch (ArgumentException exception)
        {
            if (!_disposed) Messages.Add(new(false, exception.Message));
        }
        catch (UnauthorizedAccessException)
        {
            if (!_disposed) Messages.Add(new(false, "Bạn cần đăng nhập để tra cứu dữ liệu."));
        }
        catch (Exception exception)
        {
            LogQueryFailure(Logger, exception);
            if (!_disposed) Messages.Add(new(false, "Chưa thể lấy dữ liệu lúc này. Hãy thử lại sau hoặc mở màn hình nghiệp vụ để kiểm tra."));
        }
        finally
        {
            if (Messages.Count > 40) Messages.RemoveRange(0, Messages.Count - 40);
            _busy = false;
            _scrollPending = true;
            _focusPending = true;
        }
    }

    private void Clear()
    {
        Messages.Clear();
        _scrollPending = true;
        _focusPending = true;
    }

    private static bool IsLocalLink(string href) => href.StartsWith('/') && !href.StartsWith("//", StringComparison.Ordinal)
        && !href.Contains('\\');

    [LoggerMessage(Level = LogLevel.Warning, Message = "OEE chat data query failed")]
    private static partial void LogQueryFailure(ILogger logger, Exception exception);

    public void Dispose()
    {
        _disposed = true;
        _lifetime.Cancel();
        _lifetime.Dispose();
        GC.SuppressFinalize(this);
    }
}
