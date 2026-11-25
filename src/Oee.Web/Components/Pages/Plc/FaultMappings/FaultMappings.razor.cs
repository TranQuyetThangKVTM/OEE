using MudBlazor;
using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.PlcIntegration;
using Oee.Application.PlcIntegration.Configuration.Faults.Delete;
using Oee.Application.PlcIntegration.Configuration.Faults.Queries;
using Oee.Application.PlcIntegration.Configuration.Faults.Update;
using Oee.Application.PlcIntegration.Configuration.Overview.Queries;
using Oee.Web.Components.Shared;

namespace Oee.Web.Components.Pages.Plc;

public partial class FaultMappings
{
    private PageTable<PlcFaultMappingItem>? _table;
    private PlcConfigurationLookups? _lookups;
    private string? _search;
    private string? _state;
    private string? _workUnitId;
    private string? _error;
    private int _totalCount;

    protected override async Task OnInitializedAsync()
    {
        // Lookup WorkUnit/Reason active được lấy từ Application, không hard-code trong Razor.
        try
        {
            _lookups = await Plc.ExecuteAsync(new GetPlcLookupsQuery(), CancellationToken.None);
        }
        catch (Exception)
        {
            _error = "Không thể tải danh mục fault mapping.";
        }
    }

    private async Task<TableData<PlcFaultMappingItem>> LoadAsync(
        TableState tableState,
        CancellationToken cancellationToken)
    {
        // Filter scope/state/search cùng sort/page được EF Core thực hiện trước khi trả DTO.
        try
        {
            _error = null;
            PagedResult<PlcFaultMappingItem> result = await Plc.ExecuteAsync(new ListPlcFaultMappingsQuery(new PlcListQuery(
                    _search,
                    _state,
                    ParseId(_workUnitId),
                    tableState.SortLabel,
                    tableState.SortDirection == SortDirection.Descending,
                    tableState.Page + 1,
                    tableState.PageSize)), cancellationToken);
            _totalCount = result.TotalCount;
            return new TableData<PlcFaultMappingItem> { Items = result.Items, TotalItems = result.TotalCount };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            _error = "Không thể tải fault mapping.";
            return new TableData<PlcFaultMappingItem> { Items = [], TotalItems = 0 };
        }
    }

    private Task ApplyFiltersAsync() => _table?.GoToFirstPageAndReloadAsync() ?? Task.CompletedTask;

    private Task ReloadAsync() => _table?.ReloadAsync() ?? Task.CompletedTask;

    private Task BeginAdd() => OpenEditorAsync(null);
    private Task BeginEdit(PlcFaultMappingItem item) => OpenEditorAsync(item);

    private async Task OpenEditorAsync(PlcFaultMappingItem? item)
    {
        _error = null;
        var parameters = new DialogParameters<FaultMappingEditorDialog> { { x => x.Item, item }, { x => x.Lookups, _lookups } };
        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true, BackdropClick = false, CloseOnEscapeKey = false };
        var dialog = await DialogService.ShowAsync<FaultMappingEditorDialog>(item is null ? "Thêm fault mapping" : "Sửa fault mapping", parameters, options);
        if (await dialog.Result is { Canceled: false }) await ReloadAsync();
    }

    private async Task ToggleAsync(PlcFaultMappingItem item)
    {
        // Giữ nguyên nội dung mapping, đảo IsActive và gửi RowVersion hiện tại.
        try
        {
            PlcFaultMappingRequest request = new(
                item.WorkUnitId,
                item.FaultCode,
                item.DowntimeReasonId,
                item.Description,
                !item.IsActive,
                item.RowVersion);
            await Plc.ExecuteAsync(new UpdatePlcFaultMappingCommand(item.Id, request), CancellationToken.None);
            await ReloadAsync();
        }
        catch (Exception)
        {
            _error = "Không thể đổi trạng thái mapping; bản ghi có thể đã thay đổi.";
        }
    }

    private async Task DeleteAsync(PlcFaultMappingItem item)
    {
        // Chỉ soft-delete mapping; DowntimeEvent đã phát sinh và log tích hợp vẫn được giữ.
        DialogParameters<ConfirmDialog> parameters = new()
        {
            { component => component.Title, "Xóa fault mapping" },
            { component => component.Message, $"Soft-delete {item.Scope}/{item.FaultCode}? Downtime lịch sử không bị xóa." },
            { component => component.ConfirmText, "Xóa" }
        };
        IDialogReference dialog = await DialogService.ShowAsync<ConfirmDialog>("Xác nhận", parameters);
        DialogResult? result = await dialog.Result;
        if (result is null || result.Canceled)
        {
            return;
        }

        try
        {
            await Plc.ExecuteAsync(new DeletePlcFaultMappingCommand(item.Id, item.RowVersion), CancellationToken.None);
            await ReloadAsync();
        }
        catch (Exception)
        {
            _error = "Không thể xóa fault mapping; bản ghi có thể đã thay đổi.";
        }
    }

    private async Task OpenImportAsync()
    {
        // Import chạy dry-run và transaction trong dialog; chỉ reload bảng khi DialogResult báo thành công.
        DialogOptions options = new()
        {
            FullWidth = true,
            MaxWidth = MaxWidth.Large,
            BackdropClick = false
        };
        IDialogReference dialog = await DialogService.ShowAsync<FaultMappingImportDialog>(
            "Import fault mappings",
            options: options);
        DialogResult? result = await dialog.Result;
        if (result is { Canceled: false })
        {
            await ReloadAsync();
        }
    }

    private static Guid? ParseId(string? value) => Guid.TryParse(value, out Guid id) ? id : null;
}
