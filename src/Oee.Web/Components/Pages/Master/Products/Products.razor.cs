using MudBlazor;
using Oee.Application.Catalog.Products.Delete;
using Oee.Application.Contracts.Modules;
using Oee.Web.Components.Shared;

namespace Oee.Web.Components.Pages.Master;

public partial class Products
{
    private MasterDataLookups? _lookups;
    private PageTable<ProductListItem>? _table;
    private string? _search;
    private string? _status;
    private string? _error;
    private int _totalCount;

    protected override async Task OnInitializedAsync()
    {
        // Lookup trạng thái được tải một lần khi mở trang để bộ lọc không dùng danh sách hard-code.
        try
        {
            _lookups = await Query.LookupsAsync(CancellationToken.None);
        }
        catch (Exception)
        {
            _error = "Không thể tải danh mục.";
        }
    }

    private async Task<TableData<ProductListItem>> LoadAsync(
        TableState state,
        CancellationToken cancellationToken)
    {
        // MudTable truyền trạng thái sort/page vào đây; Query sẽ dịch các điều kiện thành truy vấn SQL có paging.
        try
        {
            _error = null;
            PagedResult<ProductListItem> result = await Query.ProductsAsync(
                new MasterListQuery(
                    Search: _search,
                    Status: _status,
                    SortBy: state.SortLabel,
                    Descending: state.SortDirection == SortDirection.Descending,
                    Page: state.Page + 1,
                    PageSize: state.PageSize),
                cancellationToken);

            await SetCountAsync(result.TotalCount);
            return new TableData<ProductListItem>
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
            _error = "Không thể tải sản phẩm.";
            await InvokeAsync(StateHasChanged);
            return new TableData<ProductListItem> { Items = [], TotalItems = 0 };
        }
    }

    private Task OpenCreateDialogAsync() => OpenCreateDialogAsync(null);

    private Task CopyAsync(ProductListItem item) => OpenCreateDialogAsync(item.Id);

    private async Task OpenCreateDialogAsync(Guid? copyFrom)
    {
        // Dialog chỉ trả kết quả thành công sau khi Application đã lưu sản phẩm xuống SQL.
        var parameters = new DialogParameters<MasterDataCreateDialog>
        {
            { x => x.Kind, MasterCreateKind.Product },
            { x => x.CopyFrom, copyFrom }
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<MasterDataCreateDialog>(copyFrom.HasValue ? "Sao chép sản phẩm" : "Thêm sản phẩm", parameters, options);
        var result = await dialog.Result;
        if (result is not null && !result.Canceled) await ReloadAsync();
    }

    private async Task DeleteAsync(ProductListItem item)
    {
        // Xác nhận trước thao tác nhạy cảm. Backend vẫn kiểm tra RowVersion, quyền và quan hệ tham chiếu.
        IDialogReference dialog = await DialogService.ShowAsync<ConfirmDialog>(
            "Xác nhận",
            CreateConfirmParameters(
                "Xóa sản phẩm",
                $"Soft-delete {item.Code}? Bản ghi đang được tham chiếu sẽ bị từ chối; "
                    + "hãy chuyển Inactive thay thế.",
                "Xóa"));
        DialogResult? result = await dialog.Result;

        if (result is null || result.Canceled)
        {
            return;
        }

        try
        {
            // RowVersion giúp phát hiện trường hợp bản ghi đã được người khác cập nhật sau khi bảng được tải.
            await Commands.ExecuteAsync(new DeleteProductCommand(item.Id, item.RowVersion), CancellationToken.None);
            await ReloadAsync();
        }
        catch (Exception)
        {
            _error = "Không thể xóa sản phẩm.";
        }
    }

    private Task ApplyFiltersAsync() => _table?.GoToFirstPageAndReloadAsync() ?? Task.CompletedTask;

    private Task ReloadAsync()
    {
        // Reload giữ nguyên trạng thái filter/sort/page do PageTable đang quản lý.
        return _table?.ReloadAsync() ?? Task.CompletedTask;
    }

    private async Task SetCountAsync(int value)
    {
        // Chỉ render lại chip tổng số khi giá trị thật sự thay đổi.
        if (_totalCount == value)
        {
            return;
        }

        _totalCount = value;
        await InvokeAsync(StateHasChanged);
    }

    private static DialogParameters<ConfirmDialog> CreateConfirmParameters(
        string title,
        string message,
        string confirmText)
    {
        return new DialogParameters<ConfirmDialog>
        {
            { component => component.Title, title },
            { component => component.Message, message },
            { component => component.ConfirmText, confirmText }
        };
    }
}
