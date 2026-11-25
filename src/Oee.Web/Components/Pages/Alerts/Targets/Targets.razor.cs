using MudBlazor;
using Oee.Application.Contracts.Modules;
using Oee.Application.Oee.Alerts.Delete;
using Oee.Application.Oee.Alerts.EndTarget;
using Oee.Application.Oee.Alerts.Queries;
using Oee.Web.Components.Shared;

namespace Oee.Web.Components.Pages.Alerts;

public partial class Targets
{
    private MasterDataLookups? _lookups;
    private PageTable<TargetListItem>? _table;
    private string? _search;
    private bool _effectiveOnly = true;
    private int _totalCount;
    private string? _error;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            // Lookup Site/WorkUnit được tải một lần từ Application để editor không tạo N+1 query.
            _lookups = await MasterData.LookupsAsync(CancellationToken.None);
        }
        catch (Exception)
        {
            _error = "Không thể tải danh mục phạm vi.";
        }
    }

    private async Task<TableData<TargetListItem>> LoadAsync(
        TableState state,
        CancellationToken cancellationToken)
    {
        try
        {
            // ServerData truyền sort/page/cancellation tới query; effectiveOnly chỉ lọc phiên bản có hiệu lực.
            _error = null;
            PagedResult<TargetListItem> result = await Service.ExecuteAsync(new ListOeeTargetsQuery(new TargetListQuery(
                    _search,
                    _effectiveOnly,
                    DateOnly.FromDateTime(DateTime.Today),
                    state.SortLabel,
                    state.SortDirection != SortDirection.Ascending,
                    state.Page + 1,
                    state.PageSize)), cancellationToken);
            await SetCountAsync(result.TotalCount);
            return new TableData<TargetListItem> { Items = result.Items, TotalItems = result.TotalCount };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            _error = "Không thể tải ngưỡng mục tiêu.";
            await InvokeAsync(StateHasChanged);
            return new TableData<TargetListItem> { Items = [], TotalItems = 0 };
        }
    }

    private Task BeginAdd() => OpenEditorAsync(null);
    private Task BeginVersion(TargetListItem item) => OpenEditorAsync(item);

    private async Task OpenEditorAsync(TargetListItem? item)
    {
        _error = null;
        var parameters = new DialogParameters<OeeTargetEditorDialog> { { x => x.Item, item }, { x => x.Lookups, _lookups } };
        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true, BackdropClick = false, CloseOnEscapeKey = false };
        var dialog = await DialogService.ShowAsync<OeeTargetEditorDialog>(item is null ? "Thêm ngưỡng mục tiêu" : "Thêm phiên bản kế tiếp", parameters, options);
        if (await dialog.Result is { Canceled: false }) await ReloadAsync();
    }

    private async Task EndAsync(TargetListItem item)
    {
        // Kết thúc hiệu lực là command riêng có RowVersion, không sửa trực tiếp tỷ lệ của version cũ.
        if (!await ConfirmAsync(
            "Kết thúc hiệu lực",
            $"Kết thúc ngưỡng {item.Scope} / {item.PeriodCode}?",
            "Kết thúc"))
        {
            return;
        }

        DateOnly end = DateOnly.FromDateTime(DateTime.Today);
        if (end < item.From)
        {
            end = item.From;
        }
        await ExecuteAsync(() => Service.ExecuteAsync(new EndOeeTargetCommand(item.Id, new EndTargetRequest(end, item.RowVersion)), CancellationToken.None));
    }

    private async Task DeleteAsync(TargetListItem item)
    {
        // Backend chỉ cho soft-delete target chưa được lịch sử tham chiếu; UI không tự quyết định rule này.
        if (!await ConfirmAsync(
            "Xóa ngưỡng",
            "Chỉ ngưỡng chưa được cảnh báo lịch sử tham chiếu mới có thể soft-delete.",
            "Xóa"))
        {
            return;
        }
        await ExecuteAsync(() => Service.ExecuteAsync(new DeleteOeeTargetCommand(item.Id, item.RowVersion), CancellationToken.None));
    }

    private async Task ExecuteAsync(Func<Task> action)
    {
        // Chỉ reload khi command hoàn tất; concurrency/DB error giữ editor/page để người dùng xử lý.
        try
        {
            _error = null;
            await action();
            await ReloadAsync();
        }
        catch (Exception)
        {
            _error = "Thao tác thất bại; dữ liệu có thể đã thay đổi.";
        }
    }

    private async Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        DialogParameters<ConfirmDialog> parameters = new()
        {
            { dialog => dialog.Title, title },
            { dialog => dialog.Message, message },
            { dialog => dialog.ConfirmText, confirmText }
        };
        IDialogReference dialog = await DialogService.ShowAsync<ConfirmDialog>("Xác nhận", parameters);
        DialogResult? result = await dialog.Result;
        return result is { Canceled: false };
    }

    private Task ApplyFiltersAsync() => _table?.GoToFirstPageAndReloadAsync() ?? Task.CompletedTask;

    private Task ReloadAsync() => _table?.ReloadAsync() ?? Task.CompletedTask;

    private async Task SetCountAsync(int count)
    {
        if (_totalCount != count)
        {
            _totalCount = count;
            await InvokeAsync(StateHasChanged);
        }
    }

    private static string ScopeText(string value) => value == "Global" ? "Toàn cục" : value;

    private static string PeriodText(string value)
    {
        return value switch
        {
            "DAY" => "Ngày",
            "S1" => "S1 · Sáng",
            "S2" => "S2 · Chiều",
            "S3" => "S3 · Đêm",
            "SHIFT" => "Mọi ca",
            _ => value
        };
    }
}
