using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Oee.Application.Contracts.PlcIntegration;
using Oee.Application.PlcIntegration.Configuration.Faults.Create;
using Oee.Application.PlcIntegration.Configuration.Faults.Update;
using Oee.Application.Security;

namespace Oee.Web.Components.Pages.Plc;

public partial class FaultMappingEditorDialog
{
    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = default!;
    [Parameter] public PlcFaultMappingItem? Item { get; set; }
    [Parameter] public PlcConfigurationLookups? Lookups { get; set; }

    private PlcFaultMappingForm _form = new();
    private MudForm? _mudForm;
    private bool _valid;
    private bool _saving;
    private string? _error;
    private string Title => Item is null ? "Thêm fault mapping" : "Sửa fault mapping";

    protected override void OnInitialized()
    {
        // Chỉ sao chép DTO an toàn; secret đã lưu không được hydrate vào form.
        if (Item is not null)
        {
            _form = new PlcFaultMappingForm
            {
                WorkUnitId = Item.WorkUnitId?.ToString() ?? string.Empty,
                FaultCode = Item.FaultCode,
                ReasonId = Item.DowntimeReasonId.ToString(),
                Description = Item.Description,
                Active = Item.IsActive,
                RowVersion = Item.RowVersion,
            };
        }
    }

    private async Task SaveAsync()
    {
        if (_saving) return;
        await (_mudForm?.ValidateAsync() ?? Task.CompletedTask);
        if (!_valid) return;
        var authentication = await Authentication.GetAuthenticationStateAsync();
        if (!(await Authorization.AuthorizeAsync(authentication.User, Capabilities.PlcManage)).Succeeded)
        {
            _error = "Bạn không có quyền thực hiện thao tác này.";
            return;
        }
        if (_saving) return;
        // _saving khóa nút và backend kiểm tra lại reason, uniqueness scope/code, quyền và concurrency.
        try
        {
            _saving = true;
            _error = null;
            if (!Guid.TryParse(_form.ReasonId, out Guid reasonId))
            {
                throw new ArgumentException("Downtime reason là bắt buộc.");
            }

            PlcFaultMappingRequest request = new(
                ParseId(_form.WorkUnitId),
                _form.FaultCode,
                reasonId,
                _form.Description,
                _form.Active,
                _form.RowVersion);
            if (Item is not null)
            {
                // Update thay đúng mapping theo Id; dữ liệu downtime lịch sử không bị tái phân loại tự động.
                await Plc.ExecuteAsync(new UpdatePlcFaultMappingCommand(Item!.Id, request), CancellationToken.None);
            }
            else
            {
                await Plc.ExecuteAsync(new CreatePlcFaultMappingCommand(request), CancellationToken.None);
            }
            Dialog.Close(DialogResult.Ok(true));
        }
        catch (Exception)
        {
            _error = "Không thể lưu fault mapping; kiểm tra trùng scope/code hoặc concurrency.";
        }
        finally
        {
            _saving = false;
        }
    }


    private void Cancel() { if (!_saving) Dialog.Cancel(); }
    private static Guid? ParseId(string? value) => Guid.TryParse(value, out Guid id) ? id : null;
}
