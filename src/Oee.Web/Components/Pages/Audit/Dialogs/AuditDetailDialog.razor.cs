using Microsoft.AspNetCore.Components;
using MudBlazor;
using Oee.Application.Contracts.Audit;
using Oee.Web.Components.Presentation;

namespace Oee.Web.Components.Pages;

public partial class AuditDetailDialog
{
    [CascadingParameter]
    private IMudDialogInstance Dialog { get; set; } = default!;

    /// <summary>Id append-only của audit entry cần xem.</summary>
    [Parameter, EditorRequired]
    public Guid AuditLogId { get; set; }

    /// <summary>Query service do trang cha truyền vào; chỉ đọc DTO, không expose DbContext.</summary>
    [Parameter, EditorRequired]
    public Oee.Application.Audit.IAuditQueryService AuditService { get; set; } = default!;

    private AuditLogDetailResponse? _detail;
    private bool _loading = true;
    private string? _error;
    protected override async Task OnInitializedAsync()
    {
        // DetailAsync xử lý bản ghi cũ/JSON thiếu field và authorization RawJson ở phía server.
        try
        {
            _detail = await AuditService.DetailAsync(AuditLogId, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _error = "Không thể tải chi tiết audit. " + exception.Message;
        }
        finally
        {
            _loading = false;
        }
    }

    // Action quyết định tiêu đề cột: create chỉ có After, delete chỉ có Before, update có cả hai.
    private bool IsCreate() => _detail?.Action is "Added" or "UserCreated";
    private bool IsDelete() => _detail?.Action is "Deleted" or "UserDisabled";
    private string SectionTitle() => IsCreate() ? "Nội dung tạo mới" : IsDelete() ? "Dữ liệu trước khi xóa" : "Chi tiết thay đổi";
    private string SubjectTitle() => string.IsNullOrWhiteSpace(_detail?.EntitySubject) ? string.Empty : " · " + _detail.EntitySubject;
    // Null/rỗng được biểu diễn bằng dấu gạch để dialog không phát sinh lỗi khi đọc audit phiên bản cũ.
    private static string Value(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value;
    private static SemanticKind ActionKind(string action) => action switch
    {
        "Added" or "UserCreated" or "Activate" or "UserUnlocked" => SemanticKind.Success,
        "Deleted" or "UserDisabled" or "Deactivate" or "UserLocked" => SemanticKind.Error,
        "UserPasswordReset" or "EndEffectiveness" => SemanticKind.Warning,
        _ => SemanticKind.Info,
    };
    private void Close() => Dialog.Close();
}
