using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using Oee.Application.Catalog.Standards.EndStandardEffectiveness;
using Oee.Application.Contracts.Modules;
using Oee.Application.Security;

namespace Oee.Web.Components.Pages.Master;

public partial class EndStandardEffectivenessDialog
{
    [CascadingParameter]
    private IMudDialogInstance Dialog { get; set; } = default!;

    [Parameter, EditorRequired]
    public Guid Id { get; set; }

    private MudForm? _form;
    private WorkUnitStandardListItem? _item;
    private DateTime? _effectiveTo = DateTime.Today.AddDays(-1);
    private string? _loadError;
    private string? _saveError;
    private bool _loading = true;
    private bool _saving;
    private bool _valid;
    private DateTime? MinDate => _item?.EffectiveFrom.ToDateTime(TimeOnly.MinValue);

    protected override async Task OnInitializedAsync()
    {
        // MinDate là EffectiveFrom; default ưu tiên hôm qua nhưng không bao giờ trước ngày bắt đầu.
        try
        {
            _item = await Query.StandardAsync(Id, CancellationToken.None);
            var earliest = _item.EffectiveFrom.ToDateTime(TimeOnly.MinValue);
            _effectiveTo = _item.EffectiveTo?.ToDateTime(TimeOnly.MinValue) ?? (earliest <= DateTime.Today.AddDays(-1) ? DateTime.Today.AddDays(-1) : earliest);
        }
        catch (Exception exception)
        {
            _loadError = "Không thể tải định mức mới nhất. " + exception.Message;
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task SaveAsync()
    {
        // MudForm kiểm tra ngày; Application kiểm tra lại overlap/reference/concurrency trước khi ghi SQL/audit.
        if (_saving)
            return;
        await (_form?.ValidateAsync() ?? Task.CompletedTask);
        if (!_valid || !_effectiveTo.HasValue || _item is null)
            return;
        _saving = true;
        _saveError = null;
        try
        {
            var authentication = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            if (!(await Authorization.AuthorizeAsync(authentication.User, Capabilities.MasterDataWrite)).Succeeded)
            {
                _saveError = "Bạn không có quyền kết thúc hiệu lực định mức.";
                return;
            }

            // DTO chỉ chứa Id, EffectiveTo và RowVersion nên không thể sửa tốc độ/WorkUnit/Product qua action này.
            var result = await Commands.ExecuteAsync(new EndStandardEffectivenessCommand(Id, DateOnly.FromDateTime(_effectiveTo.Value), _item.RowVersion), CancellationToken.None);
            Snackbar.Add("Đã kết thúc hiệu lực định mức.", Severity.Success);
            Dialog.Close(DialogResult.Ok(result));
        }
        catch (Oee.Application.Common.ConcurrencyConflictException)
        {
            _saveError = "Định mức đã được người khác cập nhật. Hãy tải lại.";
        }
        catch (Exception exception)
        {
            _saveError = "Không thể kết thúc hiệu lực. " + exception.Message;
        }
        finally
        {
            _saving = false;
        }
    }

    private void Cancel() => Dialog.Cancel();
}
