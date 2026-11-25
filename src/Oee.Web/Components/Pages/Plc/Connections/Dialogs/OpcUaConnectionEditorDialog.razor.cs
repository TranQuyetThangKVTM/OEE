using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Oee.Application.Contracts.PlcIntegration;
using Oee.Application.PlcIntegration.Configuration.Connections.Create;
using Oee.Application.PlcIntegration.Configuration.Connections.Update;
using Oee.Application.Security;

namespace Oee.Web.Components.Pages.Plc;

public partial class OpcUaConnectionEditorDialog
{
    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = default!;
    [Parameter] public OpcUaConnectionItem? Item { get; set; }

    private OpcUaConnectionForm _form = new();
    private MudForm? _mudForm;
    private bool _valid;
    private bool _saving;
    private string? _error;
    private string Title => Item is null ? "Thêm kết nối OPC UA" : "Sửa kết nối OPC UA";

    protected override void OnInitialized()
    {
        // Chỉ sao chép DTO an toàn; secret đã lưu không được hydrate vào form.
        if (Item is not null)
        {
            _form = new OpcUaConnectionForm
            {
                Code = Item.Code,
                Name = Item.Name,
                Endpoint = Item.EndpointUrl,
                SecurityMode = Item.SecurityMode,
                SecurityPolicy = Item.SecurityPolicy,
                AuthenticationType = Item.AuthenticationType,
                UserName = Item.UserName,
                CertificateReference = Item.CertificateReference,
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
        // _saving chống double-click; lỗi validation/concurrency giữ editor mở để người dùng xử lý.
        try
        {
            _saving = true;
            _error = null;
            OpcUaConnectionRequest request = new(
                _form.Code,
                _form.Name,
                _form.Endpoint,
                _form.SecurityMode,
                _form.SecurityPolicy,
                _form.AuthenticationType,
                _form.UserName,
                _form.SecretReference,
                _form.ClearSecret,
                _form.CertificateReference,
                _form.Active,
                _form.RowVersion);

            if (Item is not null)
            {
                // Update gửi RowVersion và chỉ thay secret khi người dùng nhập reference mới hoặc chọn ClearSecret.
                await Plc.ExecuteAsync(new UpdateOpcUaConnectionCommand(Item!.Id, request), CancellationToken.None);
            }
            else
            {
                // Service kiểm tra endpoint/security/auth và ghi audit đã masking dữ liệu nhạy cảm.
                await Plc.ExecuteAsync(new CreateOpcUaConnectionCommand(request), CancellationToken.None);
            }

            Dialog.Close(DialogResult.Ok(true));
        }
        catch (Exception)
        {
            _error = "Không thể lưu kết nối; dữ liệu có thể không hợp lệ hoặc đã thay đổi.";
        }
        finally
        {
            _saving = false;
        }
    }


    private void Cancel() { if (!_saving) Dialog.Cancel(); }
}
