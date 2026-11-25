using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Oee.Application.Contracts.Identity;
using Oee.Application.Identity.Users.Create;
using Oee.Application.Identity.Users.Queries;
using Oee.Application.Identity.Users.Update;

namespace Oee.Web.Components.Pages;

public partial class UserEditorDialog
{
    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = default!;
    /// <summary>Null để tạo user; có giá trị để tải và cập nhật user hiện hữu.</summary>
    [Parameter] public Guid? UserId { get; set; }
    /// <summary>Id actor hiện tại dùng để khóa thay đổi role của chính mình trên giao diện.</summary>
    [Parameter] public Guid? CurrentUserId { get; set; }
    /// <summary>Diễn giải password policy lấy từ cấu hình Identity thực tế.</summary>
    [Parameter] public string PasswordPolicyText { get; set; } = "Mật khẩu phải đáp ứng chính sách bảo mật của hệ thống.";

    private MudForm? _mudForm;
    private readonly UserEditorForm _form = new();
    private IReadOnlyCollection<UserRoleResponse> _roles = [];
    private bool _loading = true;
    private bool _saving;
    private bool _valid;
    private bool _editingSelf;
    private string? _error;

    protected override async Task OnInitializedAsync()
    {
        // Tải roles trước; nếu Update thì đọc lại user theo Id và lấy concurrency token mới nhất.
        try
        {
            _roles = await UsersService.ExecuteAsync(new ListUserRolesQuery(), CancellationToken.None);
            if (UserId.HasValue)
            {
                var user = await UsersService.ExecuteAsync(new GetUserQuery(UserId.Value), CancellationToken.None);
                // Chỉ các trường an toàn trong UserResponse được copy; password/hash/security stamp không có trong DTO.
                _form.UserName = user.UserName;
                _form.DisplayName = user.DisplayName;
                _form.Email = user.Email;
                _form.ConcurrencyToken = user.ConcurrencyToken;
                _form.Roles = user.Roles.ToHashSet(StringComparer.Ordinal);
                _editingSelf = CurrentUserId == user.Id;
            }
        }
        catch (Exception exception)
        {
            _error = "Không thể tải dữ liệu tài khoản. " + exception.Message;
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task SaveAsync()
    {
        // Guard ở method cùng Disabled trên button để một dialog không gửi hai command đồng thời.
        if (_saving)
        {
            return;
        }

        // Client validation cho phản hồi sớm; Identity/Application vẫn validation toàn bộ phía server.
        await (_mudForm?.ValidateAsync() ?? Task.CompletedTask);
        if (!_valid || _form.Roles.Count == 0)
        {
            return;
        }

        _saving = true;
        _error = null;
        try
        {
            if (UserId.HasValue)
            {
                // Update gửi username để server đối chiếu/không cho đổi, cùng token optimistic concurrency.
                await UsersService.ExecuteAsync(new UpdateUserCommand(UserId.Value, new UpdateUserRequest(
                    _form.UserName!.Trim(), Normalize(_form.Email), _form.DisplayName!.Trim(),
                    _form.Roles.ToArray(), _form.ConcurrencyToken!)), CancellationToken.None);
            }
            else
            {
                // Create đưa plain password trực tiếp tới UserManager trong request sống ngắn; không tự hash ở UI.
                await UsersService.ExecuteAsync(new CreateUserCommand(new CreateUserRequest(
                    _form.UserName!.Trim(), Normalize(_form.Email), _form.DisplayName!.Trim(),
                    _form.Password!, _form.Roles.ToArray())), CancellationToken.None);
            }

            // Chỉ đóng dialog sau khi tạo/cập nhật user và roles đã hoàn tất thành công.
            Snackbar.Add(UserId.HasValue ? "Đã cập nhật tài khoản." : "Đã tạo tài khoản.", Severity.Success);
            Dialog.Close(DialogResult.Ok(true));
        }
        catch (Exception exception)
        {
            _error = Friendly(exception);
        }
        finally
        {
            _saving = false;
        }
    }

    private void SetRole(string role, bool selected)
    {
        // HashSet ngăn role trùng trong request; backend vẫn từ chối role không tồn tại/ngoài quyền actor.
        if (selected) _form.Roles.Add(role); else _form.Roles.Remove(role);
    }

    private static string? ValidateEmail(string? email)
        => string.IsNullOrWhiteSpace(email) || new EmailAddressAttribute().IsValid(email)
            ? null : "Email không đúng định dạng.";

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Friendly(Exception exception) => exception is Oee.Application.Common.ConcurrencyConflictException
        // Conflict không bị che bằng ghi đè; hướng dẫn tải lại để lấy phiên bản mới.
        ? "Tài khoản đã được thay đổi bởi người khác. Hãy đóng dialog, tải lại và thử lại."
        : "Không thể lưu tài khoản. " + exception.Message;
    private void Cancel() => Dialog.Cancel();
}
