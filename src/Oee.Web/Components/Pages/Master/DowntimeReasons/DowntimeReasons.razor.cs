using MudBlazor;
using Oee.Application.Contracts.Modules;
using Oee.Application.Downtime.Reasons.Delete;
using Oee.Web.Components.Shared;

namespace Oee.Web.Components.Pages.Master;

public partial class DowntimeReasons
{
    private MasterDataLookups? _lookups;
    private PageTable<DowntimeReasonListItem>? _table;
    private string? _search;
    private string? _category;
    private string? _status;
    private string? _error;
    private int _totalCount;

    protected override async Task OnInitializedAsync()
    {
        // Tải các danh mục Category/Kind/Status dùng chung từ Application thay vì hard-code lựa chọn trong UI.
        try
        {
            _lookups = await Query.LookupsAsync(CancellationToken.None);
        }
        catch (Exception)
        {
            _error = "Không thể thực hiện thao tác. Hãy làm mới và thử lại.";
        }
    }

    private async Task<TableData<DowntimeReasonListItem>> LoadAsync(
        TableState state,
        CancellationToken cancellationToken)
    {
        // Query thực hiện filter, sort và paging ở SQL; CancellationToken hủy truy vấn khi người dùng rời trang.
        try
        {
            _error = null;
            PagedResult<DowntimeReasonListItem> result = await Query.DowntimeReasonsAsync(
                new MasterListQuery(
                    Search: _search,
                    Category: _category,
                    Status: _status,
                    SortBy: state.SortLabel,
                    Descending: state.SortDirection == SortDirection.Descending,
                    Page: state.Page + 1,
                    PageSize: state.PageSize),
                cancellationToken);
            await SetCountAsync(result.TotalCount);
            return new TableData<DowntimeReasonListItem>
            {
                Items = result.Items,
                TotalItems = result.TotalCount
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            _error = "Không thể tải lý do dừng.";
            await InvokeAsync(StateHasChanged);
            return new TableData<DowntimeReasonListItem> { Items = [], TotalItems = 0 };
        }
    }

    private async Task OpenCreateDialogAsync()
    {
        // Dialog tạo mới chỉ đóng thành công sau khi command lưu SQL và audit hoàn tất.
        var parameters = new DialogParameters<MasterDataCreateDialog> { { x => x.Kind, MasterCreateKind.DowntimeReason } };
        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<MasterDataCreateDialog>("Thêm lý do dừng máy", parameters, options);
        var result = await dialog.Result;
        if (result is not null && !result.Canceled) await ReloadAsync();
    }

    private async Task DeleteAsync(DowntimeReasonListItem item)
    {
        // UI xin xác nhận; command mới quyết định có thể soft-delete dựa trên tham chiếu downtime/fault mapping.
        DialogParameters<ConfirmDialog> parameters = new()
        {
            { component => component.Title, "Xóa lý do dừng" },
            { component => component.Message, $"Soft-delete {item.Code}? Reason đang được downtime/fault mapping tham chiếu sẽ bị từ chối." },
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
            await Commands.ExecuteAsync(new DeleteDowntimeReasonCommand(item.Id, item.RowVersion), CancellationToken.None);
            await ReloadAsync();
        }
        catch (Exception)
        {
            _error = "Không thể xóa lý do.";
        }
    }

    private Task ApplyFiltersAsync() => _table?.GoToFirstPageAndReloadAsync() ?? Task.CompletedTask;

    private Task ReloadAsync() => _table?.ReloadAsync() ?? Task.CompletedTask;

    private async Task SetCountAsync(int value)
    {
        if (_totalCount != value)
        {
            _totalCount = value;
            await InvokeAsync(StateHasChanged);
        }
    }

}
