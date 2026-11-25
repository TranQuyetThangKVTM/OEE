using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Oee.Application.Security;
using Oee.Application.Contracts.Operations;
using Oee.Application.Production.Samples.Copy;
using Oee.Application.Production.Samples.Create;
using Oee.Application.Production.Samples.Update;

namespace Oee.Web.Components.Pages.Production;

public partial class SamplePlanEditorDialog
{
    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = default!;
    private MudForm? _mudForm;
    private bool _valid;

    [Parameter]
    public Guid Id { get; set; }

    [Parameter]
    public Guid? CopyFrom { get; set; }

    private SamplePlanHeaderForm form = new();
    private string? rowVersion;
    private bool loading = true;
    private bool _loadFailed;
    private bool saving;
    private string? error;
    private bool IsCopy => Id == Guid.Empty && CopyFrom.HasValue;
    private string Title => Id != Guid.Empty ? "Chỉnh sửa kế hoạch mẫu" : CopyFrom.HasValue ? "Sao chép kế hoạch mẫu" : "Tạo kế hoạch mẫu";

    protected override Task OnParametersSetAsync() => LoadAsync();
    private async Task LoadAsync()
    {
        // Id ưu tiên edit; copy chuẩn bị mã mới và giữ cấu trúc mẫu nguồn.
        loading = true;
        _loadFailed = false;
        error = null;
        try
        {
            var sourceId = Id != Guid.Empty ? Id : CopyFrom;
            if (sourceId.HasValue)
            {
                var source = await Query.SampleAsync(sourceId.Value, CancellationToken.None);
                form = new SamplePlanHeaderForm
                {
                    Code = IsCopy ? $"{source.Code[..Math.Min(source.Code.Length, 18)]}-COPY-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}" : source.Code,
                    Name = IsCopy ? source.Name[..Math.Min(source.Name.Length, 190)] + " - Bản sao" : source.Name,
                    Description = source.Description,
                };
                rowVersion = source.RowVersion;
            }
        }
        catch (Exception)
        {
            _loadFailed = true;
            error = "Không thể tải mẫu.";
        }
        finally
        {
            loading = false;
        }
    }

    private async Task SaveAsync()
    {
        if (saving)
            return;
        await (_mudForm?.ValidateAsync() ?? Task.CompletedTask);
        if (!_valid || saving) return;
        var authentication = await Authentication.GetAuthenticationStateAsync();
        if (!(await Authorization.AuthorizeAsync(authentication.User, Capabilities.ProductionPlanManage)).Succeeded) { error = "Bạn không có quyền lưu kế hoạch mẫu."; return; }
        if (saving) return;
        // saving chống double submit; unique code/concurrency vẫn được server kiểm tra.
        saving = true;
        error = null;
        try
        {
            var request = new SamplePlanRequest(form.Code!, form.Name!, form.Description);
            SamplePlanResponse saved;
            // Edit, copy và create là ba command path rõ ràng; copy không sửa source.
            if (Id != Guid.Empty)
                saved = await Commands.ExecuteAsync(new UpdateSamplePlanCommand(Id, request, Convert.FromBase64String(rowVersion!)), CancellationToken.None);
            else if (CopyFrom.HasValue)
            {
                saved = await Commands.ExecuteAsync(new CopySamplePlanCommand(CopyFrom.Value, form.Code!, form.Name!), CancellationToken.None);
            }
            else
                saved = await Commands.ExecuteAsync(new CreateSamplePlanCommand(request), CancellationToken.None);
            Dialog.Close(DialogResult.Ok(saved));
        }
        catch (Exception)
        {
            error = "Không thể lưu mẫu; mã có thể bị trùng hoặc dữ liệu đã thay đổi.";
        }
        finally
        {
            saving = false;
        }
    }
    private void Cancel() { if (!saving) Dialog.Cancel(); }
}
