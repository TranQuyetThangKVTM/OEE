using Microsoft.AspNetCore.Components;
using MudBlazor;
using Oee.Application.Production.LineOff;
using Oee.Web.Components.Shared;

namespace Oee.Web.Components.Pages.Dashboard.Dialogs;

public partial class LineOffPeriodsDialog : IDisposable
{
    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = default!;
    [Parameter, EditorRequired] public Guid WorkCenterId { get; set; }
    [Parameter, EditorRequired] public string Line { get; set; } = string.Empty;
    [Parameter, EditorRequired] public string TimeZoneId { get; set; } = "UTC";
    [Parameter] public DateOnly Date { get; set; }
    private readonly CancellationTokenSource _lifetime = new();
    private LineOffCalendar _calendar = LineOffCalendar.Empty;
    private DateTime? _fromDate, _untilDate;
    private TimeSpan? _fromTime, _untilTime;
    private Guid? _editingId;
    private string? _note, _error;
    private bool _loading = true, _busy, _changed;
    private IReadOnlyList<LineOffPeriod> Periods => _calendar.Periods
        .Where(x => x.WorkCenterId == WorkCenterId && !x.Deleted).OrderByDescending(x => x.Start).ToArray();

    protected override async Task OnInitializedAsync()
    {
        Reset();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        _error = null;
        try { _calendar = await Runner.ExecuteAsync(new GetLineOffCalendarQuery(), _lifetime.Token); }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch { _error = "Không thể tải lịch Off. Hãy thử tải lại."; }
        finally { _loading = false; }
    }

    private async Task SaveAsync()
    {
        if (_busy || _loading) return;
        _error = null;
        if (!_fromDate.HasValue || !_untilDate.HasValue || !_fromTime.HasValue || !_untilTime.HasValue)
        { _error = "Hãy nhập đầy đủ ngày và giờ bắt đầu/kết thúc."; return; }
        _busy = true;
        try
        {
            _calendar = await Runner.ExecuteAsync(new SaveLineOffPeriodCommand(WorkCenterId,
                _fromDate.Value.Date.Add(_fromTime.Value), _untilDate.Value.Date.Add(_untilTime.Value),
                _note, _calendar.Revision, _editingId), _lifetime.Token);
            _changed = true;
            Reset();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception exception) when (exception is ArgumentException or UnauthorizedAccessException) { _error = exception.Message; }
        catch { _error = "Không thể lưu lịch Off. Hãy tải lại và thử lại."; }
        finally { _busy = false; }
    }

    private void Edit(LineOffPeriod period)
    {
        var start = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(period.Start, TimeZoneId);
        var end = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(period.End, TimeZoneId);
        _editingId = period.Id;
        _fromDate = start.Date;
        _fromTime = start.TimeOfDay;
        _untilDate = end.Date;
        _untilTime = end.TimeOfDay;
        _note = period.Note;
        _error = null;
    }

    private async Task DeleteAsync(LineOffPeriod period)
    {
        if (_busy) return;
        var parameters = new DialogParameters<ConfirmDialog>
        {
            { x => x.Title, "Xóa thời gian Off" },
            { x => x.Message, $"Xóa khoảng Off {LocalText(period.Start)} – {LocalText(period.End)}? OEE Line sẽ tính lại khoảng này theo lịch sản xuất." },
            { x => x.ConfirmText, "Xóa" }
        };
        var confirmation = await DialogService.ShowAsync<ConfirmDialog>("Xóa thời gian Off", parameters);
        if (await confirmation.Result is not { Canceled: false }) return;
        _busy = true;
        _error = null;
        try
        {
            _calendar = await Runner.ExecuteAsync(new DeleteLineOffPeriodCommand(period.Id, _calendar.Revision), _lifetime.Token);
            _changed = true;
            if (_editingId == period.Id) Reset();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception exception) when (exception is ArgumentException or UnauthorizedAccessException) { _error = exception.Message; }
        catch { _error = "Không thể xóa lịch Off. Hãy tải lại và thử lại."; }
        finally { _busy = false; }
    }

    private void Reset()
    {
        _editingId = null;
        _fromDate = _untilDate = Date.ToDateTime(TimeOnly.MinValue);
        _fromTime = _untilTime = null;
        _note = null;
    }

    private string LocalText(DateTimeOffset value) => TimeZoneInfo.ConvertTimeBySystemTimeZoneId(value, TimeZoneId)
        .ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.CurrentCulture);
    private void Close() => Dialog.Close(DialogResult.Ok(_changed));
    public void Dispose() { _lifetime.Cancel(); _lifetime.Dispose(); GC.SuppressFinalize(this); }
}
