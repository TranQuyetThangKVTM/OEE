using System.Globalization;
using MudBlazor;
using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.PlcIntegration;
using Oee.Application.PlcIntegration.Configuration.Connections.Delete;
using Oee.Application.PlcIntegration.Configuration.Connections.Queries;
using Oee.Application.PlcIntegration.Configuration.Connections.TestConnection;
using Oee.Web.Components.Shared;

namespace Oee.Web.Components.Pages.Plc;

public partial class OpcUaConnections
{
    private PageTable<OpcUaConnectionItem>? _table;
    private string? _search;
    private string? _state;
    private string? _error;
    private int _totalCount;

    private async Task<TableData<OpcUaConnectionItem>> LoadAsync(
        TableState tableState,
        CancellationToken cancellationToken)
    {
        // Search/state/sort/page đi xuống Application/EF Core; chỉ DTO cần hiển thị được trả về MudTable.
        try
        {
            _error = null;
            PagedResult<OpcUaConnectionItem> result = await Plc.ExecuteAsync(new ListOpcUaConnectionsQuery(new PlcListQuery(
                    _search,
                    _state,
                    SortBy: tableState.SortLabel,
                    Descending: tableState.SortDirection == SortDirection.Descending,
                    Page: tableState.Page + 1,
                    PageSize: tableState.PageSize)), cancellationToken);
            _totalCount = result.TotalCount;
            return new TableData<OpcUaConnectionItem> { Items = result.Items, TotalItems = result.TotalCount };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            _error = "Không thể tải kết nối.";
            return new TableData<OpcUaConnectionItem> { Items = [], TotalItems = 0 };
        }
    }

    private Task ApplyFiltersAsync() => _table?.GoToFirstPageAndReloadAsync() ?? Task.CompletedTask;

    private Task ReloadAsync() => _table?.ReloadAsync() ?? Task.CompletedTask;

    private Task BeginAdd() => OpenEditorAsync(null);
    private Task BeginEdit(OpcUaConnectionItem item) => OpenEditorAsync(item);

    private async Task OpenEditorAsync(OpcUaConnectionItem? item)
    {
        _error = null;
        var parameters = new DialogParameters<OpcUaConnectionEditorDialog> { { x => x.Item, item } };
        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true, BackdropClick = false, CloseOnEscapeKey = false };
        var dialog = await DialogService.ShowAsync<OpcUaConnectionEditorDialog>(item is null ? "Thêm kết nối OPC UA" : "Sửa kết nối OPC UA", parameters, options);
        if (await dialog.Result is { Canceled: false }) await ReloadAsync();
    }

    private async Task TestAsync(Guid connectionId)
    {
        // Backend tự resolve secret/certificate và trả kết quả đã làm sạch; UI không nhận credential.
        try
        {
            _error = null;
            OpcUaConnectionTestResult result = await Plc.ExecuteAsync(new TestOpcUaConnectionCommand(connectionId), CancellationToken.None);
            DialogParameters<ConfirmDialog> parameters = new()
            {
                { component => component.Title, "Kết quả test connection" },
                { component => component.Message, $"{result.State}: {result.Message}" },
                { component => component.ConfirmText, "Đóng" },
                { component => component.ConfirmColor, result.Success ? Color.Success : Color.Warning }
            };
            await DialogService.ShowAsync<ConfirmDialog>("Test OPC UA", parameters);
        }
        catch (Exception)
        {
            _error = "Không thể test connection.";
        }
    }

    private async Task DeleteAsync(OpcUaConnectionItem item)
    {
        // Yêu cầu soft delete có xác nhận; connection đang được mapping tham chiếu sẽ bị từ chối.
        DialogParameters<ConfirmDialog> parameters = new()
        {
            { component => component.Title, "Xóa kết nối" },
            { component => component.Message, $"Soft-delete kết nối {item.Code}? Kết nối đang được thiết bị tham chiếu sẽ bị từ chối." },
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
            await Plc.ExecuteAsync(new DeleteOpcUaConnectionCommand(item.Id, item.RowVersion), CancellationToken.None);
            await ReloadAsync();
        }
        catch (Exception)
        {
            _error = "Không thể xóa kết nối.";
        }
    }

    private static string FormatTimestamp(DateTimeOffset? value)
    {
        // Timestamp từ backend có offset/UTC; chỉ chuyển sang giờ local khi trình bày.
        return value?.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.CurrentCulture) ?? "—";
    }
}
