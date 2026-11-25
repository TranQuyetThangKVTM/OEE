using MudBlazor;
using Oee.Application.Contracts.Modules;
using Oee.Application.Organizations.Shifts.Delete;
using Oee.Web.Components.Shared;

namespace Oee.Web.Components.Pages.Master;

public partial class Shifts
{
    private MasterDataLookups? _lookups;
    private PageTable<ShiftListItem>? _table;
    private string? _search;
    private string? _siteId;
    private string? _status;
    private string? _error;
    private int _totalCount;

    protected override async Task OnInitializedAsync()
    {
        // Lấy danh sách Site và trạng thái từ SQL qua Query Service để phục vụ bộ lọc/form.
        try
        {
            _lookups = await Query.LookupsAsync(CancellationToken.None);
        }
        catch (Exception)
        {
            _error = "Không thể thực hiện thao tác. Hãy làm mới và thử lại.";
        }
    }

    private async Task<TableData<ShiftListItem>> LoadAsync(TableState state, CancellationToken cancellationToken)
    {
        // PageTable chuyển search/filter/sort/page xuống EF Core, tránh tải toàn bộ ca vào browser.
        try
        {
            _error = null;
            PagedResult<ShiftListItem> result = await Query.ShiftsAsync(
                new MasterListQuery(
                    Search: _search,
                    ParentId: ParseId(_siteId),
                    Status: _status,
                    SortBy: state.SortLabel,
                    Descending: state.SortDirection == SortDirection.Descending,
                    Page: state.Page + 1,
                    PageSize: state.PageSize),
                cancellationToken);
            await SetCountAsync(result.TotalCount);
            return new TableData<ShiftListItem> { Items = result.Items, TotalItems = result.TotalCount };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            _error = "Không thể tải ca.";
            await InvokeAsync(StateHasChanged);
            return new TableData<ShiftListItem> { Items = [], TotalItems = 0 };
        }
    }

    private async Task OpenCreateDialogAsync()
    {
        // Chỉ reload bảng khi dialog báo thành công; dialog thất bại vẫn mở để người dùng sửa dữ liệu.
        var parameters = new DialogParameters<MasterDataCreateDialog> { { x => x.Kind, MasterCreateKind.Shift } };
        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<MasterDataCreateDialog>("Thêm ca làm việc", parameters, options);
        var result = await dialog.Result;
        if (result is not null && !result.Canceled) await ReloadAsync();
    }

    private async Task DeleteAsync(ShiftListItem item)
    {
        // Backend chỉ soft-delete ca chưa bị dữ liệu lịch sử tham chiếu; dialog là bước xác nhận UX.
        DialogParameters<ConfirmDialog> parameters = new()
        {
            { component => component.Title, "Xóa ca" },
            { component => component.Message, $"Soft-delete ca {item.Code}? Ca có kế hoạch/sản lượng sẽ bị từ chối." },
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
            await Commands.ExecuteAsync(new DeleteShiftCommand(item.Id, item.RowVersion), CancellationToken.None);
            await ReloadAsync();
        }
        catch (Exception)
        {
            _error = "Không thể xóa ca.";
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

    private static Guid? ParseId(string? value) => Guid.TryParse(value, out Guid id) ? id : null;

}
