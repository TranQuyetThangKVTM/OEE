using Microsoft.AspNetCore.Components;
using MudBlazor;
using Oee.Application.Audit.Retention.PreviewRetention;
using Oee.Application.Audit.Retention.RunRetention;
using Oee.Application.Contracts.Audit;

namespace Oee.Web.Components.Pages;

public partial class AuditRetentionDialog
{
    [CascadingParameter]
    private IMudDialogInstance Dialog { get; set; } = default!;

    private AuditRetentionPreview? _preview;
    private bool _loading = true;
    private bool _saving;
    private string? _error;
    protected override async Task OnInitializedAsync()
    {
        // Preview trả candidate count tại thời điểm mở; RunRetentionAsync sẽ kiểm tra/tính lại khi thực thi.
        try
        {
            _preview = await MaintenanceService.ExecuteAsync(new PreviewRetentionAuditQuery(), CancellationToken.None);
        }
        catch (Exception exception)
        {
            _error = "Không thể xem trước retention. " + exception.Message;
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task RunAsync()
    {
        // Guard cùng Disabled ở button chống gửi hai retention request từ một dialog.
        if (_saving)
            return;
        _saving = true;
        _error = null;
        try
        {
            // Request không chứa số tháng/cutoff tùy ý; backend dùng options đã validate và authorization riêng.
            var result = await MaintenanceService.ExecuteAsync(new RunRetentionAuditCommand(new AuditRetentionRequest()), CancellationToken.None);
            Snackbar.Add($"Đã dọn {result.DeletedCount:N0}/{result.CandidateCount:N0} bản ghi cũ.", Severity.Success);
            Dialog.Close(DialogResult.Ok(result));
        }
        // Lỗi giữ dialog mở, không báo thành công hoặc reload bảng khi DeletedCount chưa được xác nhận.
        catch (Exception exception)
        {
            _error = "Không thể chạy retention. " + exception.Message;
        }
        finally
        {
            _saving = false;
        }
    }

    private void Cancel() => Dialog.Cancel();
}
