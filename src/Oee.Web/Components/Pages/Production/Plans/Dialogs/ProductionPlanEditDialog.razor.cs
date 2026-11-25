using Microsoft.AspNetCore.Components;
using MudBlazor;
using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.Operations;
using Oee.Application.Production.Plans.Correct;
using Oee.Application.Production.Plans.Common;
using Oee.Application.Production.Plans.Queries;
using Oee.Application.Production.Plans.Update;

namespace Oee.Web.Components.Pages.Production;

public partial class ProductionPlanEditDialog
{
    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = default!;
    [Parameter] public Guid Id { get; set; }
    private MudForm? _form;
    private ProductionLookups? _lookups;
    private ProductionPlanResponse? _existing;
    private Guid? _shiftId;
    private DateTime? _startDate;
    private DateTime? _endDate;
    private TimeSpan? _startTime;
    private TimeSpan? _endTime;
    private DateTime _originalDate;
    private decimal _quantity;
    private string? _notes;
    private string? _error;
    private bool _loading = true;
    private bool _saving;

    private IEnumerable<ProductionLookupOption> AvailableShifts => _lookups?.Shifts.Where(x =>
        x.SiteId == _lookups.WorkUnits.FirstOrDefault(unit => unit.Id == _existing?.WorkUnitId)?.SiteId) ?? [];

    private string PlanCode
    {
        get
        {
            try { return BuildPlanCode(); }
            catch (InvalidOperationException) { return string.Empty; }
        }
    }

    private string BuildPlanCode()
    {
        if (_existing is null) return string.Empty;
        var suffix = _existing.HasOutput ? $"-V{_existing.Version + 1}" : string.Empty;
        if (_startDate.HasValue && (_startDate.Value.Date != _originalDate || _shiftId != _existing.ShiftId))
        {
            var product = _lookups?.Products.FirstOrDefault(item => item.Id == _existing.ProductId);
            var machine = _lookups?.WorkUnits.FirstOrDefault(item => item.Id == _existing.WorkUnitId);
            var shift = AvailableShifts.FirstOrDefault(item => item.Id == _shiftId);
            if (product is not null && machine is not null && shift is not null)
            {
                return ProductionPlanCode.Format(product.Code, DateOnly.FromDateTime(_startDate.Value), shift.Code, machine.Name, suffix);
            }
        }

        var code = _existing.Code;
        return code[..Math.Min(code.Length, 32 - suffix.Length)] + suffix;
    }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _lookups = await Query.LookupsAsync(CancellationToken.None);
            _existing = await Commands.ExecuteAsync(new GetProductionPlanQuery(Id), CancellationToken.None);
            var start = TimeZoneInfo.ConvertTime(_existing.PlannedStart, Zone()).DateTime;
            var end = TimeZoneInfo.ConvertTime(_existing.PlannedEnd, Zone()).DateTime;
            _startDate = _originalDate = start.Date;
            _endDate = end.Date;
            _startTime = start.TimeOfDay;
            _endTime = end.TimeOfDay;
            _shiftId = _existing.ShiftId;
            _quantity = _existing.PlannedQuantity;
            _notes = _existing.Notes;
        }
        catch (Exception) { _existing = null; _error = "Không thể tải kế hoạch. Hãy thử lại."; }
        finally { _loading = false; }
    }

    private void ShiftChanged(Guid? value) { _shiftId = value; ApplyShift(); }
    private void StartDateChanged(DateTime? value) { _startDate = value; ApplyShift(); }
    private void ApplyShift()
    {
        var shift = AvailableShifts.FirstOrDefault(x => x.Id == _shiftId);
        if (_startDate is null || shift?.StartTime is null || shift.EndTime is null) return;
        _startTime = shift.StartTime.Value.ToTimeSpan();
        _endTime = shift.EndTime.Value.ToTimeSpan();
        _endDate = _startDate.Value.Date.AddDays(_endTime <= _startTime ? 1 : 0);
    }

    private async Task SaveAsync()
    {
        if (_saving || _existing is null) return;
        await (_form?.ValidateAsync() ?? Task.CompletedTask);
        if (_form?.IsValid != true) return;
        _error = null;
        if (!_startDate.HasValue || !_endDate.HasValue || !_startTime.HasValue || !_endTime.HasValue || _quantity <= 0)
        { _error = "Vui lòng nhập đủ thời gian và sản lượng lớn hơn 0."; return; }
        _saving = true;
        try
        {
            var start = ToUtc(_startDate.Value.Date + _startTime.Value);
            var end = ToUtc(_endDate.Value.Date + _endTime.Value);
            if (end <= start) { _error = "Thời gian kết thúc phải sau thời gian bắt đầu."; return; }
            var request = new ProductionPlanRequest(_existing.WorkUnitId, _existing.ProductId, _shiftId, BuildPlanCode(),
                start, end, _quantity, _notes);
            var token = Convert.FromBase64String(_existing.RowVersion);
            var saved = _existing.HasOutput
                ? await Commands.ExecuteAsync(new CorrectProductionPlanCommand(Id, request, token), CancellationToken.None)
                : await Commands.ExecuteAsync(new UpdateProductionPlanCommand(Id, request, token), CancellationToken.None);
            Snackbar.Add("Đã cập nhật kế hoạch.", Severity.Success);
            Dialog.Close(DialogResult.Ok(saved));
        }
        catch (Oee.Application.Common.ConcurrencyConflictException) { _error = "Kế hoạch đã thay đổi. Hãy đóng popup, làm mới danh sách rồi sửa lại."; }
        catch (InvalidOperationException exception) { _error = exception.Message; }
        catch (ArgumentException exception) { _error = exception.Message; }
        catch (Exception) { _error = "Không thể lưu kế hoạch. Hãy kiểm tra kết nối hoặc thử lại."; }
        finally { _saving = false; }
    }

    private TimeZoneInfo Zone()
    {
        var zoneId = _lookups?.WorkUnits.FirstOrDefault(x => x.Id == _existing?.WorkUnitId)?.Detail
            ?? throw new InvalidOperationException("Không tìm thấy múi giờ thiết bị.");
        return TimeZoneInfo.FindSystemTimeZoneById(zoneId);
    }
    private DateTimeOffset ToUtc(DateTime value)
    {
        var local = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
        var zone = Zone();
        if (zone.IsInvalidTime(local)) throw new ArgumentException("Thời gian không hợp lệ trong múi giờ thiết bị.");
        return new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
    }
    private static string LookupText(IEnumerable<ProductionLookupOption> items, Guid id)
    {
        var item = items.FirstOrDefault(x => x.Id == id);
        return item is null ? "Không còn hoạt động" : $"{item.Code} — {item.Name}";
    }
    private static string ShiftText(ProductionLookupOption shift) => shift.StartTime.HasValue && shift.EndTime.HasValue
        ? $"{shift.Code} — {shift.Name} ({shift.StartTime:HH:mm}–{shift.EndTime:HH:mm})" : $"{shift.Code} — {shift.Name}";
    private void Cancel() => Dialog.Cancel();
}
