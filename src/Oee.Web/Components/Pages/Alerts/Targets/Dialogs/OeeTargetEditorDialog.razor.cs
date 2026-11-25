using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.Operations;
using Oee.Application.Oee.Targets.AddTargetVersion;
using Oee.Application.Security;

namespace Oee.Web.Components.Pages.Alerts;

public partial class OeeTargetEditorDialog
{
    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = default!;
    [Parameter] public TargetListItem? Item { get; set; }
    [Parameter] public MasterDataLookups? Lookups { get; set; }

    private OeeTargetForm _form = new();
    private MudForm? _mudForm;
    private bool _valid;
    private bool _saving;
    private string? _error;
    private string Title => Item is null ? "Thêm ngưỡng mục tiêu" : "Thêm phiên bản kế tiếp";

    protected override void OnInitialized()
    {
        // Chỉ sao chép DTO an toàn; secret đã lưu không được hydrate vào form.
        if (Item is not null)
        {
            _form = new OeeTargetForm
            {
                ScopeType = ResolveScopeType(Item),
                ScopeId = (Item.WorkUnitId ?? Item.SiteId)?.ToString(),
                PeriodCode = Item.PeriodCode,
                Oee = Item.Oee * 100,
                Availability = Item.Availability * 100,
                Performance = Item.Performance * 100,
                Quality = Item.Quality * 100,
                From = NextVersionDate(Item),
                Note = Item.Note,
            };
        }
        else { _form = new OeeTargetForm { From = DateTime.Today, Oee = 75, Availability = 85, Performance = 90, Quality = 99 }; }
    }

    private async Task SaveAsync()
    {
        if (_saving) return;
        await (_mudForm?.ValidateAsync() ?? Task.CompletedTask);
        if (!_valid) return;
        var authentication = await Authentication.GetAuthenticationStateAsync();
        if (!(await Authorization.AuthorizeAsync(authentication.User, Capabilities.TargetsManage)).Succeeded)
        {
            _error = "Bạn không có quyền thực hiện thao tác này.";
            return;
        }
        if (_saving) return;
        // Client validation cải thiện UX; Application/DB vẫn validate lại scope, ratio và overlap hiệu lực.
        Guid? scopeId = ParseId(_form.ScopeId);
        if (_form.ScopeType != "Global" && !scopeId.HasValue)
        {
            _error = "Vui lòng chọn phạm vi.";
            return;
        }

        if (!_form.From.HasValue
            || new[] { _form.Oee, _form.Availability, _form.Performance, _form.Quality }
                .Any(value => value is < 0 or > 100)
            || _form.To?.Date < _form.From.Value.Date)
        {
            _error = "Tỷ lệ hoặc khoảng hiệu lực không hợp lệ.";
            return;
        }

        _saving = true;
        try
        {
            bool isDay = _form.PeriodCode == "DAY";
            // Chia 100 để chuyển giá trị UI 0..100 sang invariant database/domain 0..1.
            EffectiveTargetRequest request = new(
                _form.ScopeType == "WorkUnit" ? scopeId : null,
                _form.ScopeType == "Site" ? scopeId : null,
                isDay ? OeePeriodTypeContract.Day : OeePeriodTypeContract.Shift,
                _form.Oee / 100,
                _form.Availability / 100,
                _form.Performance / 100,
                _form.Quality / 100,
                DateOnly.FromDateTime(_form.From.Value),
                _form.To.HasValue ? DateOnly.FromDateTime(_form.To.Value) : null,
                _form.Note,
                _form.PeriodCode);
            await Service.ExecuteAsync(new AddTargetVersionCommand(request), CancellationToken.None);
            Dialog.Close(DialogResult.Ok(true));
        }
        catch (Exception)
        {
            _error = "Không thể lưu mục tiêu; khoảng hiệu lực có thể chồng lấn.";
        }
        finally
        {
            _saving = false;
        }
    }


    private void Cancel() { if (!_saving) Dialog.Cancel(); }
    private static Guid? ParseId(string? value) => Guid.TryParse(value, out Guid id) ? id : null;

    private static string ResolveScopeType(TargetListItem item)
    {
        return item.WorkUnitId.HasValue ? "WorkUnit" : item.SiteId.HasValue ? "Site" : "Global";
    }

    private static DateTime NextVersionDate(TargetListItem item)
    {
        DateTime currentStart = item.From.ToDateTime(TimeOnly.MinValue);
        return DateTime.Today > currentStart
            ? DateTime.Today
            : item.From.AddDays(1).ToDateTime(TimeOnly.MinValue);
    }

}
