using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using MudBlazor;
using Oee.Application.Contracts.Reporting;
using Oee.Application.Reporting.Importing.FaultMappings.Import;
using Oee.Application.Reporting.Importing.FaultMappings.Preview;

namespace Oee.Web.Components.Pages.Plc;

public partial class FaultMappingImportDialog
{
    private const long MaximumBytes = 5_000_000;

    // Giới hạn 5 MB được áp dụng ngay lúc đọc browser stream để giảm rủi ro file nén tiêu tốn tài nguyên.
    private byte[]? _fileBytes;
    private FaultMappingImportPreview? _preview;
    private FaultMappingImportResult? _result;
    private bool _loading;
    private string? _error;
    private string? _selectedFileName;

    [CascadingParameter]
    private IMudDialogInstance Dialog { get; set; } = default!;

    private void DownloadTemplate()
    {
        // forceLoad gọi endpoint tải file nhị phân thay vì để Blazor router xử lý route API.
        Navigation.NavigateTo("/api/v1/plc/fault-mappings/import/template", forceLoad: true);
    }

    private async Task PreviewFileAsync(IBrowserFile? file)
    {
        if (_loading || file is null) return;
        // Bước dry-run đọc file trong giới hạn, kiểm tra version/row/duplicate nhưng chưa thay đổi database.
        try
        {
            _loading = true;
            _error = null;
            _result = null;

            _selectedFileName = file.Name;
            await using Stream input = file.OpenReadStream(MaximumBytes);
            using MemoryStream memory = new();
            await input.CopyToAsync(memory);
            _fileBytes = memory.ToArray();

            using MemoryStream previewStream = new(_fileBytes);
            // Stream mới bắt đầu từ vị trí 0 để service có thể đọc toàn bộ workbook.
            _preview = await Importer.ExecuteAsync(new PreviewFaultMappingsQuery(previewStream, _fileBytes.LongLength), CancellationToken.None);
        }
        catch (Exception exception)
        {
            _fileBytes = null;
            _preview = null;
            _error = "Không thể đọc file XLSX. " + exception.Message;
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task CommitAsync()
    {
        // Không commit nếu chưa có file; nút cũng bị disabled khi preview còn lỗi.
        if (_fileBytes is null)
        {
            return;
        }

        try
        {
            _loading = true;
            _error = null;
            using MemoryStream stream = new(_fileBytes);
            // Backend validation lại toàn bộ dữ liệu trong transaction vì trạng thái SQL có thể đổi sau dry-run.
            _result = await Importer.ExecuteAsync(new ImportFaultMappingsCommand(stream, _fileBytes.LongLength), CancellationToken.None);

            if (_result.Issues.Count > 0)
            {
                // Giữ dialog mở để người dùng xem lỗi; không báo thành công khi transaction bị từ chối.
                _error = "Import bị từ chối do dữ liệu thay đổi sau dry-run.";
            }
            else
            {
                // Trang cha nhận DialogResult.Ok để reload bảng mapping từ SQL.
                Dialog.Close(DialogResult.Ok(_result));
            }
        }
        catch (Exception exception)
        {
            _error = "Import thất bại và transaction đã rollback. " + exception.Message;
        }
        finally
        {
            _loading = false;
        }
    }

    private void Cancel()
    {
        Dialog.Cancel();
    }
}
