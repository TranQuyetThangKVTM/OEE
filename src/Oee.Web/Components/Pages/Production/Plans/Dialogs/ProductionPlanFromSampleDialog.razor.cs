using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.Operations;
using Oee.Application.Production.Plans.Create;
using Oee.Application.Production.Plans.PreviewBulk;
using Oee.Application.Security;
using Oee.Web.Components.Presentation;

namespace Oee.Web.Components.Pages.Production;

/// <summary>Chỉ tạo từ mẫu; không có danh mục chọn thiết bị, SKU hay chế độ Direct.</summary>
public partial class ProductionPlanFromSampleDialog : IDisposable
{
    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = default!;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Dictionary<Guid, decimal> _overrides = [];
    private CancellationTokenSource? _pendingPreview;
    private MudForm? _form;
    private SamplePlanListItem? _selectedSample;
    private DateTime? _appliedDate = DateTime.Today;
    private string? _notes;
    private string? _error;
    private BulkProductionPlanPreview? _preview;
    private bool _formValid;
    private bool _loadingPreview;
    private bool _saving;
    private bool _disposed;
    private int _previewVersion;
    private bool CanSave => !_saving && !_loadingPreview && _selectedSample is not null
        && _appliedDate.HasValue && _preview is { ValidCount: > 0 };

    private async Task OnSampleChangedAsync(SamplePlanListItem? value)
    {
        if (_disposed || _saving || _selectedSample == value) return;
        _selectedSample = value;
        _overrides.Clear();
        await RefreshPreviewAsync();
    }

    private async Task OnDateChangedAsync(DateTime? value)
    {
        if (_disposed || _saving || _appliedDate == value) return;
        _appliedDate = value;
        await RefreshPreviewAsync();
    }

    private async Task<IEnumerable<SamplePlanListItem>> SearchSamplesAsync(string? value, CancellationToken ct)
    {
        try { return await Runner.SearchSamplesAsync(value, ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return []; }
        catch { _error = "Không thể tải kế hoạch mẫu. Hãy tìm lại hoặc kiểm tra kết nối."; return []; }
    }

    private async Task RefreshPreviewAsync()
    {
        var version = ++_previewVersion;
        _pendingPreview?.Cancel();
        _preview = null;
        _error = null;
        _loadingPreview = false;
        if (_selectedSample is null || !_appliedDate.HasValue) return;
        using var request = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _pendingPreview = request;
        _loadingPreview = true;
        try
        {
            var preview = await Runner.ExecuteAsync(new PreviewProductionPlansQuery(BuildRequest()), request.Token);
            if (!_disposed && !request.IsCancellationRequested && version == _previewVersion) _preview = preview;
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested) { }
        catch (Exception exception)
        {
            if (version == _previewVersion) _error = "Không thể tạo bản xem trước. " + exception.Message;
        }
        finally
        {
            if (version == _previewVersion) _loadingPreview = false;
            if (ReferenceEquals(_pendingPreview, request)) _pendingPreview = null;
        }
    }

    private async Task SaveAsync()
    {
        if (!CanSave) return;
        _saving = true;
        _error = null;
        try
        {
            await (_form?.ValidateAsync() ?? Task.CompletedTask);
            if (!_formValid) return;
            var authentication = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            if (!(await Authorization.AuthorizeAsync(authentication.User, Capabilities.ProductionPlanManage)).Succeeded)
            { _error = "Bạn không có quyền tạo kế hoạch sản xuất."; return; }

            var request = BuildRequest();
            // Kiểm tra lại ngay trước ghi vì kế hoạch hiện hành có thể đã thay đổi kể từ preview.
            _preview = await Runner.ExecuteAsync(new PreviewProductionPlansQuery(request), _lifetime.Token);
            if (_preview.ValidCount == 0)
            { _error = "Không còn dòng hợp lệ để tạo. Hãy kiểm tra các dòng trùng hoặc xung đột trong bản xem trước."; return; }
            var result = await Runner.ExecuteAsync(new CreateProductionPlansCommand(request), _lifetime.Token);
            Snackbar.Add($"Đã tạo {result.CreatedCount} kế hoạch từ mẫu; bỏ qua {result.SkippedCount}, xung đột {result.ConflictCount}.", Severity.Success);
            Dialog.Close(DialogResult.Ok(result));
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception exception) { _error = "Không thể tạo kế hoạch từ mẫu. " + exception.Message; }
        finally { _saving = false; }
    }

    private BulkProductionPlanRequest BuildRequest() => new(BulkPlanCreationModeContract.FromSample, [], [], null,
        _selectedSample!.Id, DateOnly.FromDateTime(_appliedDate!.Value), null, _notes,
        _overrides.Select(item => new SamplePlanQuantityOverride(item.Key, item.Value)).ToArray());

    private void SetOverride(BulkPlanCombination row, decimal value)
    {
        if (_saving || !row.SourceSamplePlanItemId.HasValue) return;
        _overrides[row.SourceSamplePlanItemId.Value] = value;
        if (_preview is not null)
            _preview = _preview with { Combinations = _preview.Combinations.Select(item =>
                item.SourceSamplePlanItemId == row.SourceSamplePlanItemId ? item with { PlannedQuantity = value } : item).ToArray() };
    }

    private void Cancel() => Dialog.Cancel();
    private static string SampleText(SamplePlanListItem? value) => value is null ? string.Empty : $"{value.Code} — {value.Name} ({value.ItemCount} dòng)";
    private static string StateText(string state) => state switch { "Valid" => "Sẽ tạo", "Skipped" => "Đã có", "Conflict" => "Xung đột", _ => "Không hợp lệ" };
    private static SemanticKind StateKind(string state) => state switch { "Valid" => SemanticKind.Success, "Conflict" => SemanticKind.Error, _ => SemanticKind.Warning };

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        _lifetime.Dispose();
        GC.SuppressFinalize(this);
    }
}
