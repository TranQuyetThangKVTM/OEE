using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.Operations;
using Oee.Application.Production.Plans.Create;
using Oee.Application.Production.Plans.PreviewBulk;
using Oee.Application.Security;

namespace Oee.Web.Components.Pages.Production;

/// <summary>Chỉ tạo trực tiếp từ thiết bị × ca × SKU; không giữ state hoặc nhánh tạo từ mẫu.</summary>
public partial class ProductionPlanCreateDialog : IDisposable
{
    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = default!;
    private readonly CancellationTokenSource _lifetime = new();
    private MudForm? _form;
    private ProductionLookups? _lookups;
    private IReadOnlyCollection<Guid> _selectedWorkUnits = [];
    private IReadOnlyCollection<Guid> _selectedShifts = [];
    private Guid? _productId;
    private decimal? _quantity = 10000m;
    private DateTime? _appliedDate = DateTime.Today;
    private string? _notes;
    private string? _error;
    private bool _formValid;
    private bool _loading = true;
    private bool _saving;

    protected override async Task OnInitializedAsync()
    {
        try { _lookups = await Runner.LookupsAsync(_lifetime.Token); }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch { _error = "Không thể tải danh mục thiết bị, SKU và ca. Hãy mở lại popup để thử lại."; }
        finally { _loading = false; }
    }

    private async Task SaveAsync()
    {
        if (_loading || _saving) return;
        _saving = true;
        _error = null;
        try
        {
            await (_form?.ValidateAsync() ?? Task.CompletedTask);
            if (!_formValid || _selectedWorkUnits.Count == 0 || _selectedShifts.Count == 0
                || !_productId.HasValue || !_appliedDate.HasValue || _quantity is null or <= 0)
            { _error = "Hãy chọn thiết bị, SKU, ca, ngày áp dụng và nhập sản lượng lớn hơn 0."; return; }

            var authentication = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            if (!(await Authorization.AuthorizeAsync(authentication.User, Capabilities.ProductionPlanManage)).Succeeded)
            { _error = "Bạn không có quyền tạo kế hoạch sản xuất."; return; }

            var request = new BulkProductionPlanRequest(BulkPlanCreationModeContract.Direct,
                _selectedWorkUnits.Distinct().ToArray(), _selectedShifts.Distinct().ToArray(), _productId,
                null, DateOnly.FromDateTime(_appliedDate.Value), _quantity, _notes);
            var preview = await Runner.ExecuteAsync(new PreviewProductionPlansQuery(request), _lifetime.Token);
            if (preview.ValidCount == 0)
            {
                _error = "Không còn tổ hợp hợp lệ để tạo. " + string.Join(" ", preview.Combinations
                    .Select(row => row.Reason).Where(reason => !string.IsNullOrWhiteSpace(reason)).Distinct());
                return;
            }
            var result = await Runner.ExecuteAsync(new CreateProductionPlansCommand(request), _lifetime.Token);
            Snackbar.Add($"Đã tạo {result.CreatedCount} kế hoạch; bỏ qua {result.SkippedCount}, xung đột {result.ConflictCount}.", Severity.Success);
            Dialog.Close(DialogResult.Ok(result));
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception exception) { _error = "Không thể tạo kế hoạch. " + exception.Message; }
        finally { _saving = false; }
    }

    private static string ShiftLabel(ProductionLookupOption option) => option.StartTime.HasValue && option.EndTime.HasValue
        ? $"{option.Code} — {option.Name} ({option.StartTime:HH:mm}–{option.EndTime:HH:mm})" : $"{option.Code} — {option.Name}";
    private void Cancel() => Dialog.Cancel();
    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
        GC.SuppressFinalize(this);
    }
}
