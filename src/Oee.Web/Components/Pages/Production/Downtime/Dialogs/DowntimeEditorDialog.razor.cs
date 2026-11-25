using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Oee.Application.Security;
using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.Operations;
using Oee.Application.Downtime.Events.Close;
using Oee.Application.Downtime.Events.Create;
using Oee.Application.Downtime.Events.Reclassify;
using Oee.Web.Components.Shared;

namespace Oee.Web.Components.Pages.Production;

public partial class DowntimeEditorDialog
{
    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = default!;
    private MudForm? _mudForm;
    private bool _valid;
    private bool _saving;
    private bool _canWrite;

    // Form state dùng scalar/DTO, không truyền EF Entity tracking vào Razor.
    [Parameter]
    public Guid Id { get; set; }

    private ProductionLookups? lookups;
    private DowntimeListItem? detail;
    private string? workUnitId;
    private string? reasonId;
    private DateTime startLocal = DateTime.Now.AddMinutes(-5);
    private DateTime endLocal = DateTime.Now;
    private bool isOpen = true;
    private string? note;
    private bool loading = true;
    private string? error;
    // Route Id mới kích hoạt load lookup/detail. Existing detail được projection read-only từ Application.
    protected override Task OnParametersSetAsync() => LoadAsync();
    private async Task LoadAsync()
    {
        loading = true;
        error = null;
        try
        {
            _canWrite = await CanWriteAsync();
            lookups = await Query.LookupsAsync(CancellationToken.None);
            if (Id != Guid.Empty)
            {
                detail = await Query.DowntimeAsync(Id, CancellationToken.None);
                workUnitId = detail.WorkUnitId.ToString();
                reasonId = detail.DowntimeReasonId.ToString();
                startLocal = Local(detail.StartAt, detail.WorkUnitId);
                endLocal = detail.EndAt.HasValue ? Local(detail.EndAt.Value, detail.WorkUnitId) : DateTime.Now;
                isOpen = !detail.EndAt.HasValue;
                note = detail.Note;
            }
        }
        catch (Exception)
        {
            error = "Không thể tải downtime.";
        }
        finally
        {
            loading = false;
        }
    }

    private async Task<bool> StartMutationAsync()
    {
        if (_saving) return false;
        if (!await CanWriteAsync())
        {
            error = "Bạn không có quyền ghi nhận hoặc thay đổi sự kiện dừng máy.";
            return false;
        }
        await (_mudForm?.ValidateAsync() ?? Task.CompletedTask);
        if (!_valid || _saving) return false;
        _saving = true;
        error = null;
        return true;
    }

    // EndAt null giữ đúng sự kiện đang mở; UTC theo timezone của thiết bị.
    private async Task CreateAsync()
    {
        if (!await StartMutationAsync()) return;
        try
        {
            if (!Guid.TryParse(workUnitId, out var unit) || !Guid.TryParse(reasonId, out var reason))
            {
                error = "Vui lòng chọn thiết bị và lý do.";
                return;
            }
            var request = new DowntimeRequest(unit, reason, Utc(startLocal, unit), isOpen ? null : Utc(endLocal, unit), null, note);
            var id = await Commands.ExecuteAsync(new CreateDowntimeEventCommand(request), CancellationToken.None);
            Dialog.Close(DialogResult.Ok(id));
        }
        catch (Exception) { error = "Không thể tạo downtime; time window có thể trùng sự kiện hiện có."; }
        finally { _saving = false; }
    }

    // Chỉ gửi reason và concurrency token, không ghi đè time window lịch sử.
    private async Task ReclassifyAsync()
    {
        if (!await StartMutationAsync()) return;
        try
        {
            if (detail is null || !Guid.TryParse(reasonId, out var reason)) return;
            var dialog = await DialogService.ShowAsync<ConfirmDialog>("Xác nhận", Confirm("Tái phân loại downtime", "Chỉ thay đổi reason/category/kind; time window được giữ nguyên và audit đầy đủ.", "Tái phân loại", Color.Primary));
            if (await dialog.Result is not { Canceled: false }) return;
            await Commands.ExecuteAsync(new ReclassifyDowntimeEventCommand(detail.Id, reason, detail.ConcurrencyToken), CancellationToken.None);
            Dialog.Close(DialogResult.Ok(detail.Id));
        }
        catch (Exception) { error = "Không thể tái phân loại; dữ liệu có thể đã thay đổi."; }
        finally { _saving = false; }
    }

    private async Task CloseAsync()
    {
        if (!await StartMutationAsync()) return;
        try
        {
            if (detail is null) return;
            var dialog = await DialogService.ShowAsync<ConfirmDialog>("Xác nhận", Confirm("Đóng sự kiện downtime", "Đặt EndAt bằng thời điểm hiện tại?", "Đóng sự kiện", Color.Error));
            if (await dialog.Result is not { Canceled: false }) return;
            await Commands.ExecuteAsync(new CloseDowntimeEventCommand(detail.Id, DateTimeOffset.UtcNow, detail.ConcurrencyToken), CancellationToken.None);
            Dialog.Close(DialogResult.Ok(detail.Id));
        }
        catch (Exception) { error = "Không thể đóng downtime; dữ liệu có thể đã thay đổi."; }
        finally { _saving = false; }
    }

    private static DialogParameters<ConfirmDialog> Confirm(string title, string message, string text, Color color) => new()
    {
        {
            x => x.Title,
            title
        },
        {
            x => x.Message,
            message
        },
        {
            x => x.ConfirmText,
            text
        },
        {
            x => x.ConfirmColor,
            color
        }
    };
    // Local chỉ để hiển thị/input; Utc dùng timezone Site và chặn local time không tồn tại trong DST trước khi lưu.
    private DateTime Local(DateTimeOffset value, Guid unit) => TimeZoneInfo.ConvertTime(value, Zone(unit)).DateTime;
    private DateTimeOffset Utc(DateTime value, Guid unit)
    {
        var local = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
        var zone = Zone(unit);
        if (zone.IsInvalidTime(local))
            throw new ArgumentException("Thời gian DST không hợp lệ.");
        return new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
    }

    private TimeZoneInfo Zone(Guid unit) => TimeZoneInfo.FindSystemTimeZoneById(lookups?.WorkUnits.First(x => x.Id == unit).Detail ?? TimeZoneInfo.Utc.Id);
    private void Cancel() { if (!_saving) Dialog.Cancel(); }
    private async Task<bool> CanWriteAsync()
    {
        var authentication = await Authentication.GetAuthenticationStateAsync();
        return (await Authorization.AuthorizeAsync(authentication.User, Capabilities.DowntimeWrite)).Succeeded;
    }
}
