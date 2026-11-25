using Microsoft.AspNetCore.Components;
using MudBlazor;
using System.Globalization;
using Oee.Application.Contracts.Modules;
using Oee.Application.Production.Automation;

namespace Oee.Web.Components.Chat;

public partial class ProductionPlanScheduleDialog : IDisposable
{
    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = default!;
    private readonly CancellationTokenSource _lifetime = new();
    private ProductionPlanSchedule? _schedule;
    private SamplePlanListItem? _sample;
    private TimeSpan? _time = TimeSpan.FromHours(16);
    private bool _enabled;
    private bool _pdfEnabled;
    private int _pdfDelay = 5;
    private bool _loading = true;
    private bool _loadFailed;
    private bool _saving;
    private string? _error;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _schedule = await Runner.ScheduleAsync(_lifetime.Token);
            if (_schedule is not null)
            {
                _enabled = _schedule.Enabled;
                _pdfEnabled = _schedule.PdfEnabled;
                _pdfDelay = _schedule.PdfDelayMinutes;
                _time = _schedule.RunAt.ToTimeSpan();
                var options = await Runner.SearchSamplesAsync(_schedule.SamplePlanCode, _lifetime.Token);
                _sample = options.FirstOrDefault(item => item.Id == _schedule.SamplePlanId);
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception exception)
        {
            _loadFailed = true;
            ShowError(exception);
        }
        finally { _loading = false; }
    }

    private static string SampleLabel(SamplePlanListItem? sample) => sample is null ? string.Empty : $"{sample.Code} · {sample.Name}";

    private async Task<IEnumerable<SamplePlanListItem>> SearchSamplesAsync(string? search, CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token);
        try { return await Runner.SearchSamplesAsync(search, linked.Token); }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { return []; }
        catch (Exception exception) { ShowError(exception); return []; }
    }

    private async Task SaveAsync()
    {
        if (_saving || _loadFailed) return;
        _error = null;
        if ((_enabled || _pdfEnabled || _schedule is null) && (_sample is null || _time is null))
        { _error = "Hãy chọn kế hoạch mẫu và giờ chạy."; return; }
        if (_time is { } time && (time < TimeSpan.Zero || time >= TimeSpan.FromDays(1)))
        { _error = "Giờ chạy phải nằm trong khoảng 00:00–23:59."; return; }
        _saving = true;
        try
        {
            var saved = await Runner.SaveScheduleAsync(new SaveProductionPlanScheduleCommand(
                _sample?.Id ?? _schedule!.SamplePlanId, TimeOnly.FromTimeSpan(_time ?? TimeSpan.FromHours(16)),
                _enabled, _schedule?.Revision, _pdfEnabled, _pdfDelay), _lifetime.Token);
            Dialog.Close(DialogResult.Ok(saved));
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception exception) { ShowError(exception); }
        finally { _saving = false; }
    }

    private string LocalTime(DateTimeOffset value) => _schedule is null ? value.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
        : TimeZoneInfo.ConvertTimeBySystemTimeZoneId(value, _schedule.TimeZoneId).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    private void ShowError(Exception exception)
    {
        LogFailure(Logger, exception);
        _error = exception switch
        {
            ArgumentException => exception.Message,
            UnauthorizedAccessException => "Không thể lưu lịch. Kiểm tra quyền quản lý kế hoạch và quyền ghi thư mục App_Data trên máy chủ.",
            _ => "Không thể đọc hoặc lưu lịch. Hãy thử lại; kiểm tra log và thư mục dữ liệu trên máy chủ nếu lỗi tiếp diễn."
        };
    }

    private void Cancel() => Dialog.Cancel();
    [LoggerMessage(Level = LogLevel.Warning, Message = "Production plan automation dialog failed")]
    private static partial void LogFailure(ILogger logger, Exception exception);

    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
        GC.SuppressFinalize(this);
    }
}
