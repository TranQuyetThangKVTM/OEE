using System.Security.Claims;
using MudBlazor;
using Oee.Application.Contracts.Identity;
using Oee.Application.Identity.Users.Disable;
using Oee.Application.Identity.Users.Queries;
using Oee.Application.Security;
using Oee.Web.Components.Presentation;
using Oee.Web.Components.Shared;

namespace Oee.Web.Components.Pages;

public partial class Users
{
    private PageTable<UserResponse>? _table;
    private IReadOnlyCollection<UserRoleResponse> _roles = [];
    private readonly HashSet<Guid> _busy = [];
    private string? _search;
    private string? _role;
    private string? _state;
    private string? _error;
    private int _totalCount;
    private Guid? _currentUserId;

    protected override async Task OnInitializedAsync()
    {
        // Lấy Id tài khoản đang đăng nhập từ claim để chặn tự đặt lại mật khẩu hoặc vô hiệu hóa trên UI.
        var state = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        if (Guid.TryParse(state.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) _currentUserId = id;
        // Vai trò hợp lệ đến từ RoleManager/Application, không hard-code danh sách checkbox trong Razor.
        try { _roles = await UsersService.ExecuteAsync(new ListUserRolesQuery(), CancellationToken.None); }
        catch (Exception) { _error = "Không thể tải vai trò."; }
    }

    private async Task<TableData<UserResponse>> LoadAsync(TableState state, CancellationToken ct)
    {
        // UserSearchRequest mang filter/sort/page xuống service; query roles được tối ưu để tránh N+1.
        try
        {
            _error = null;
            var result = await UsersService.ExecuteAsync(new SearchUsersQuery(new UserSearchRequest(
                _search, _role, _state, state.SortLabel, state.SortDirection == SortDirection.Descending,
                state.Page + 1, state.PageSize)), ct);
            await InvokeAsync(() => _totalCount = result.TotalCount);
            return new() { Items = result.Items, TotalItems = result.TotalCount };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            _error = "Không thể tải danh sách người dùng.";
            await InvokeAsync(StateHasChanged);
            return new() { Items = [], TotalItems = 0 };
        }
    }

    private Task CreateAsync() => ShowEditorAsync(null);
    private Task EditAsync(Guid userId) => ShowEditorAsync(userId);
    private async Task ShowEditorAsync(Guid? userId)
    {
        // UserId null là Create; có Id là Update và dialog sẽ tải lại user thật thay vì tin dữ liệu dòng bảng.
        var parameters = new DialogParameters<UserEditorDialog>
        {
            { dialog => dialog.UserId, userId }, { dialog => dialog.CurrentUserId, _currentUserId },
            { dialog => dialog.PasswordPolicyText, PasswordPolicyText() },
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseButton = true };
        var dialog = await DialogService.ShowAsync<UserEditorDialog>(
            userId.HasValue ? "Chỉnh sửa tài khoản" : "Tạo tài khoản", parameters, options);
        // Chỉ reload khi dialog đóng bằng DialogResult.Ok sau khi Identity/SQL đã lưu thành công.
        if (await dialog.Result is { Canceled: false }) await ReloadAsync();
    }

    private async Task ResetPasswordAsync(UserResponse user)
    {
        // Chỉ truyền Id, username hiển thị và concurrency token; password/hash cũ không bao giờ đi vào UI.
        var parameters = new DialogParameters<ResetPasswordDialog>
        {
            { dialog => dialog.UserId, user.Id }, { dialog => dialog.UserName, user.UserName },
            { dialog => dialog.ConcurrencyToken, user.ConcurrencyToken },
            { dialog => dialog.PasswordPolicyText, PasswordPolicyText() },
        };
        var dialog = await DialogService.ShowAsync<ResetPasswordDialog>(
            $"Đặt lại mật khẩu: {user.UserName}", parameters,
            new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true });
        if (await dialog.Result is { Canceled: false }) await ReloadAsync();
    }

    private async Task DisableAsync(UserResponse user)
    {
        // Disable/soft state giữ nguyên AuditLog và lịch sử nghiệp vụ, không hard delete Identity user.
        if (!await ConfirmAsync("Vô hiệu hóa tài khoản",
                $"Vô hiệu hóa {user.UserName} — {user.DisplayName}? Tài khoản không bị xóa cứng và lịch sử audit được giữ nguyên.",
                "Vô hiệu hóa", Color.Error)) return;
        await ExecuteRowAsync(user, () => UsersService.ExecuteAsync(new DisableUserCommand(user.Id, user.ConcurrencyToken), CancellationToken.None),
            "Đã vô hiệu hóa tài khoản.");
    }

    private async Task ExecuteRowAsync(UserResponse user, Func<Task> action, string success)
    {
        // HashSet theo UserId chống double-click hoặc hai action đồng thời trên cùng một dòng.
        if (!_busy.Add(user.Id)) return;
        _error = null;
        try { await action(); Snackbar.Add(success, Severity.Success); await ReloadAsync(); }
        catch (Exception) { _error = "Không thể thực hiện thao tác."; }
        finally { _busy.Remove(user.Id); }
    }

    private async Task<bool> ConfirmAsync(string title, string message, string confirmText, Color color)
    {
        var parameters = new DialogParameters<ConfirmDialog>
        {
            { dialog => dialog.Title, title }, { dialog => dialog.Message, message },
            { dialog => dialog.ConfirmText, confirmText }, { dialog => dialog.ConfirmColor, color },
        };
        return await (await DialogService.ShowAsync<ConfirmDialog>(title, parameters)).Result is { Canceled: false };
    }

    private Task ApplyFiltersAsync() => _table?.GoToFirstPageAndReloadAsync() ?? Task.CompletedTask;
    // Refresh thường giữ page/filter; áp dụng filter mới quay về trang đầu để tránh trang rỗng ngoài phạm vi.
    private Task ReloadAsync() => _table?.ReloadAsync() ?? Task.CompletedTask;
    private bool IsSelf(UserResponse user) => _currentUserId == user.Id;
    private bool IsBusy(UserResponse user) => _busy.Contains(user.Id);
    private string PasswordPolicyText()
    {
        // Sinh mô tả từ chính options đã bind cho Identity để UI không hướng dẫn sai policy server.
        var option = SecurityOptions.Value;
        var requirements = new List<string> { $"tối thiểu {option.RequiredLength} ký tự" };
        if (option.RequireUppercase) requirements.Add("một chữ hoa");
        if (option.RequireLowercase) requirements.Add("một chữ thường");
        if (option.RequireDigit) requirements.Add("một chữ số");
        if (option.RequireNonAlphanumeric) requirements.Add("một ký tự đặc biệt");
        return "Yêu cầu: " + string.Join(", ", requirements) + ".";
    }

    private static string StateText(UserResponse user) => user.IsDisabled ? "Vô hiệu hóa" : user.IsLocked ? "Đang khóa" : "Bình thường";
    private static SemanticKind StateKind(UserResponse user) => user.IsDisabled || user.IsLocked ? SemanticKind.Error : SemanticKind.Success;
    private static SemanticKind RoleKind(string role) => role switch
    {
        // Đây chỉ là màu hiển thị role, không quyết định authorization.
        RoleCapabilityMatrix.Admin => SemanticKind.Error,
        RoleCapabilityMatrix.PlanningManager => SemanticKind.Warning,
        RoleCapabilityMatrix.Operator => SemanticKind.Info,
        _ => SemanticKind.Neutral,
    };
}
