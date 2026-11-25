using MudBlazor;
using Oee.Application.Catalog.Standards.Delete;
using Oee.Application.Contracts.Modules;
using Oee.Web.Components.Shared;

namespace Oee.Web.Components.Pages.Master;

public partial class WorkUnitStandards
{
    private MasterDataLookups? _lookups;
    private PageTable<WorkUnitStandardListItem>? _table;
    private readonly TableGrouping<WorkUnitStandardListItem> _grouping = new(
        item => new(item.WorkUnitId, item.WorkUnit), item => new(item.ProductId, item.Product));
    private string? _search;
    private string? _workUnitId;
    private string? _productId;
    private bool _effectiveOnly = true;
    private string? _error;
    private int _totalCount;

    protected override async Task OnInitializedAsync()
    {
        // Lookup thiết bị và sản phẩm được tải từ dữ liệu nền thật, không tạo danh sách cứng trong Razor.
        try
        {
            _lookups = await Query.LookupsAsync(CancellationToken.None);
        }
        catch (Exception)
        {
            _error = "Không thể thực hiện thao tác. Hãy làm mới và thử lại.";
        }
    }

    private async Task<TableData<WorkUnitStandardListItem>> LoadAsync(
        TableState state,
        CancellationToken cancellationToken)
    {
        // Lọc, sắp xếp và phân trang được gửi xuống Application/EF Core để không tải toàn bộ lịch sử lên trình duyệt.
        try
        {
            _error = null;
            PagedResult<WorkUnitStandardListItem> result = await Query.StandardsAsync(
                new MasterListQuery(
                    Search: _search,
                    ParentId: ParseId(_workUnitId),
                    RelatedId: ParseId(_productId),
                    EffectiveOnly: _effectiveOnly,
                    SortBy: state.SortLabel,
                    Descending: state.SortDirection == SortDirection.Descending,
                    Page: state.Page + 1,
                    PageSize: state.PageSize),
                cancellationToken);
            await SetCountAsync(result.TotalCount);
            return new TableData<WorkUnitStandardListItem>
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
            _error = "Không thể tải định mức.";
            await InvokeAsync(StateHasChanged);
            return new TableData<WorkUnitStandardListItem> { Items = [], TotalItems = 0 };
        }
    }

    private Task OpenCreateDialogAsync() => OpenCreateDialogAsync(null);

    private Task CopyAsync(WorkUnitStandardListItem item) => OpenCreateDialogAsync(item.Id);

    private async Task OpenCreateDialogAsync(Guid? copyFrom)
    {
        // Dialog dùng luồng tạo phiên bản mới; định mức cũ không bị ghi đè.
        var parameters = new DialogParameters<MasterDataCreateDialog>
        {
            { x => x.Kind, MasterCreateKind.WorkUnitStandard },
            { x => x.CopyFrom, copyFrom }
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<MasterDataCreateDialog>(copyFrom.HasValue ? "Sao chép định mức tốc độ" : "Thêm định mức tốc độ", parameters, options);
        var result = await dialog.Result;
        if (result is not null && !result.Canceled) await ReloadAsync();
    }

    private async Task DeleteAsync(WorkUnitStandardListItem item)
    {
        // Đây là yêu cầu xóa mềm có xác nhận, không xóa cứng lịch sử đã được tham chiếu.
        DialogParameters<ConfirmDialog> parameters = new()
        {
            { component => component.Title, "Xóa phiên bản định mức" },
            { component => component.Message, "Chỉ phiên bản chưa được dữ liệu sản xuất tham chiếu mới có thể soft-delete." },
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
            await Commands.ExecuteAsync(new DeleteStandardCommand(item.Id, item.RowVersion), CancellationToken.None);
            await ReloadAsync();
        }
        catch (Exception)
        {
            _error = "Không thể xóa định mức.";
        }
    }

    private async Task EndEffectivenessAsync(WorkUnitStandardListItem item)
    {
        // Dialog chuyên biệt tải lại bản ghi và gửi RowVersion cùng ngày kết thúc hiệu lực.
        var parameters = new DialogParameters<EndStandardEffectivenessDialog> { { x => x.Id, item.Id } };
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<EndStandardEffectivenessDialog>("Kết thúc hiệu lực định mức", parameters, options);
        var result = await dialog.Result;
        if (result is not null && !result.Canceled) await ReloadAsync();
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
