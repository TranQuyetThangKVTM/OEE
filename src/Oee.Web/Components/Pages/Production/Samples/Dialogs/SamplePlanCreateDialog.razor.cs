using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.Operations;
using Oee.Application.Production.Samples.Create;
using Oee.Application.Security;

namespace Oee.Web.Components.Pages.Production;

public partial class SamplePlanCreateDialog
{
    [CascadingParameter]
    private IMudDialogInstance Dialog { get; set; } = default!;

    private MudForm? _form;
    private ProductionLookups? _lookups;
    private string? _name;
    private string? _description;
    private IReadOnlyCollection<Guid> _workUnitIds = [];
    private IReadOnlyCollection<Guid> _shiftIds = [];
    private Guid? _productId;
    private decimal _quantity = 10000m;
    private string? _notes;
    private string? _error;
    private bool _valid;
    private bool _saving;
    // Số dòng xem trước là tích Descartes của hai tập đã loại duplicate.
    private int LineCount => _workUnitIds.Distinct().Count() * _shiftIds.Distinct().Count();

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

    private async Task SaveAsync()
    {
        // Guard/Disabled/spinner chống double-click trong UI; database unique constraint chống request đồng thời.
        if (_saving)
            return;
        await (_form?.ValidateAsync() ?? Task.CompletedTask);
        if (!_valid || !_productId.HasValue || _quantity <= 0 || LineCount == 0)
            return;
        _saving = true;
        _error = null;
        try
        {
            // Kiểm tra UI cho thông báo sớm; command vẫn phải enforce capability server-side.
            var authentication = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            if (!(await Authorization.AuthorizeAsync(authentication.User, Capabilities.ProductionPlanManage)).Succeeded)
            {
                _error = "Bạn không có quyền tạo kế hoạch mẫu.";
                return;
            }

            // Một bulk request duy nhất giúp service dùng một transaction/SaveChanges thay vì lưu từng dòng vòng lặp.
            var result = await Commands.ExecuteAsync(new CreateSamplePlanWithItemsCommand(new SamplePlanBulkCreateRequest(_name ?? string.Empty, _description, _workUnitIds.Distinct().ToArray(), _shiftIds.Distinct().ToArray(), _productId.Value, _quantity, _notes)), CancellationToken.None);
            Snackbar.Add($"Đã tạo mẫu với {result.CreatedCount} dòng.", Severity.Success);
            Dialog.Close(DialogResult.Ok(result));
        }
        catch (Exception exception)
        {
            _error = "Không thể tạo kế hoạch mẫu. " + exception.Message;
        }
        finally
        {
            _saving = false;
        }
    }

    private void Cancel() => Dialog.Cancel();
}
