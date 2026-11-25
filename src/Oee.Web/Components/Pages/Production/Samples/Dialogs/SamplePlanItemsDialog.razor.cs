using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.Operations;
using Oee.Application.Production.Samples.AddItems;
using Oee.Application.Production.Samples.PreviewAddItems;
using Oee.Application.Security;

namespace Oee.Web.Components.Pages.Production;

public partial class SamplePlanItemsDialog
{
    [CascadingParameter]
    private IMudDialogInstance Dialog { get; set; } = default!;

    [Parameter, EditorRequired]
    public Guid SamplePlanId { get; set; }

    private MudForm? _form;
    private ProductionLookups? _lookups;
    private IReadOnlyCollection<Guid> _workUnitIds = [];
    private IReadOnlyCollection<Guid> _shiftIds = [];
    private Guid? _productId;
    private decimal _quantity = 10000m;
    private string? _notes;
    private string? _error;
    private bool _valid;
    private bool _saving;
    private bool _previewing;
    private SamplePlanBulkPreview? _preview;
    private int MaximumCount => _workUnitIds.Distinct().Count() * _shiftIds.Distinct().Count();

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _lookups = await Query.LookupsAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            _error = "Không thể tải danh mục đang hoạt động. " + exception.Message;
        }
    }

    private async Task PreviewAsync()
    {
        // MudForm validation chạy trước để không gửi request thiếu lookup/quantity.
        await (_form?.ValidateAsync() ?? Task.CompletedTask);
        if (!_valid || !_productId.HasValue || MaximumCount == 0)
            return;
        _previewing = true;
        _error = null;
        try
        {
            _preview = await Commands.ExecuteAsync(new PreviewSamplePlanItemsQuery(SamplePlanId, Request()), CancellationToken.None);
        }
        catch (Exception exception)
        {
            _preview = null;
            _error = "Không thể xem trước. " + exception.Message;
        }
        finally
        {
            _previewing = false;
        }
    }

    private async Task SaveAsync()
    {
        // Guard đầu method + disabled button ngăn hai request cùng lúc từ double-click.
        if (_saving)
            return;
        _saving = true;
        _error = null;
        try
        {
            var authentication = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            if (!(await Authorization.AuthorizeAsync(authentication.User, Capabilities.ProductionPlanManage)).Succeeded)
            {
                _error = "Bạn không có quyền thêm dòng kế hoạch mẫu.";
                return;
            }

            // Preview lại trước commit vì dữ liệu có thể thay đổi sau lần xem trước đầu tiên.
            _preview = await Commands.ExecuteAsync(new PreviewSamplePlanItemsQuery(SamplePlanId, Request()), CancellationToken.None);
            if (_preview.NewCount == 0)
            {
                _error = "Tất cả tổ hợp đã tồn tại trong mẫu.";
                return;
            }

            var result = await Commands.ExecuteAsync(new AddSamplePlanItemsCommand(SamplePlanId, Request()), CancellationToken.None);
            Snackbar.Add($"Đã thêm {result.CreatedCount} dòng; bỏ qua {result.SkippedCount} dòng trùng.", Severity.Success);
            Dialog.Close(DialogResult.Ok(result));
        }
        catch (Exception exception)
        {
            _error = "Không thể thêm dòng. " + exception.Message;
        }
        finally
        {
            _saving = false;
        }
    }

    private SamplePlanBulkAddItemsRequest Request() => new(_workUnitIds.Distinct().ToArray(), _shiftIds.Distinct().ToArray(), _productId ?? Guid.Empty, _quantity, _notes);
    private void Cancel() => Dialog.Cancel();
}
