using Microsoft.AspNetCore.Components;
using MudBlazor;
using Oee.Application.Contracts.Identity;
using Oee.Application.Identity.Users.ResetPassword;

namespace Oee.Web.Components.Pages;

public partial class ResetPasswordDialog
{
    [CascadingParameter]
    private IMudDialogInstance Dialog { get; set; } = default!;

    /// <summary>Khóa Guid của user cần đặt lại mật khẩu.</summary>
    [Parameter, EditorRequired]
    public Guid UserId { get; set; }

    /// <summary>Username chỉ dùng để xác nhận đối tượng trên dialog.</summary>
    [Parameter, EditorRequired]
    public string UserName { get; set; } = string.Empty;

    /// <summary>Token concurrency để request cũ không ghi đè trạng thái user mới hơn.</summary>
    [Parameter, EditorRequired]
    public string ConcurrencyToken { get; set; } = string.Empty;

    /// <summary>Mô tả password policy thực tế được trang cha cung cấp.</summary>
    [Parameter]
    public string PasswordPolicyText { get; set; } = "Mật khẩu phải đáp ứng chính sách bảo mật của hệ thống.";

    private MudForm? _form;
    private string? _password;
    private string? _confirmation;
    private string? _error;
    private bool _valid;
    private bool _saving;
    private bool CanSubmit => !string.IsNullOrWhiteSpace(_password) && !string.IsNullOrWhiteSpace(_confirmation) && string.Equals(_password, _confirmation, StringComparison.Ordinal);

    private async Task SaveAsync()
    {
        // Guard đồng thời với Disabled ở nút để double-click không tạo hai request reset.
        if (_saving)
            return;
        // MudForm kiểm tra required/matching trước; Identity tiếp tục kiểm tra policy ở server.
        await (_form?.ValidateAsync() ?? Task.CompletedTask);
        if (!_valid)
            return;
        _saving = true;
        _error = null;
        try
        {
            // Service dùng UserManager, reset AccessFailedCount và ghi audit không chứa password/hash.
            await UsersService.ExecuteAsync(new ResetUserPasswordCommand(UserId, new ResetUserPasswordRequest(_password!, ConcurrencyToken)), CancellationToken.None);
            Snackbar.Add($"Đã đặt lại mật khẩu cho {UserName}.", Severity.Success);
            Dialog.Close(DialogResult.Ok(true));
        }
        catch (Exception exception)
        {
            // Giữ dialog mở để người quản trị sửa; chỉ đóng sau khi command thành công.
            _error = "Không thể đặt lại mật khẩu. " + exception.Message;
        }
        finally
        {
            _saving = false;
        }
    }

    private string? ValidateConfirmation(string? value) => string.Equals(_password, value, StringComparison.Ordinal) ? null : "Hai mật khẩu phải giống nhau.";
    private void Cancel() => Dialog.Cancel();
}
