using System.Globalization;
using MudBlazor;
using Oee.Application.Contracts.Modules;
using Oee.Web.Components.Shared;

namespace Oee.Web.Components.Pages.Production;

public partial class DowntimeEvents
{
    private ProductionLookups? _lookups;
    private PageTable<DowntimeListItem>? _table;
    private readonly Dictionary<Guid, DowntimeListItem> _selected = [];
    private string? _search;
    private string? _workUnitId;
    private string? _reasonId;
    private bool _unknownOnly = true;
    private bool _openOnly;
    private DateTime? _fromDate = DateTime.Today.AddDays(-7);
    private DateTime? _untilDate = DateTime.Today;
    private string? _error;
    private int _totalCount;
    private int _unknownCount;
    private int _openCount;
    private bool _initializing = true;

    private IEnumerable<ProductionLookupOption> AvailableBulkReasons =>
        _lookups?.DowntimeReasons.Where(reason => reason.Code != "DR-UNKNOWN") ?? [];

    private string UnknownRate => _totalCount == 0
        // Không có mẫu số thì hiển thị “—”, không tạo NaN/Infinity.
        ? "—"
        : ((decimal)_unknownCount / _totalCount).ToString("P1", CultureInfo.CurrentCulture);

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _lookups = await Query.LookupsAsync(CancellationToken.None);
            await LoadStatsAsync();
        }
        catch (Exception)
        {
            _error = "Không thể thực hiện thao tác. Hãy làm mới và thử lại.";
        }
        finally { _initializing = false; }
    }

    private DowntimeListQuery BuildQuery(
        int page,
        int pageSize,
        string? sortBy,
        bool descending,
        bool? unknownOnly = null,
        bool? openOnly = null)
    {
        return new DowntimeListQuery(
            _search,
            ParseId(_workUnitId),
            ParseId(_reasonId),
            unknownOnly ?? _unknownOnly,
            openOnly ?? _openOnly,
            StartOfDay(_fromDate),
            EndOfDay(_untilDate),
            sortBy,
            descending,
            page,
            pageSize);
    }

    private async Task<TableData<DowntimeListItem>> LoadAsync(
        TableState state,
        CancellationToken cancellationToken)
    {
        try
        {
            // Selection chỉ có ý nghĩa trong page hiện tại nên được reset khi paging/filter reload.
            _selected.Clear();
            _error = null;
            PagedResult<DowntimeListItem> result = await Query.DowntimeAsync(
                BuildQuery(
                    state.Page + 1,
                    state.PageSize,
                    state.SortLabel,
                    state.SortLabel is null || state.SortDirection == SortDirection.Descending),
                cancellationToken);
            return new TableData<DowntimeListItem> { Items = result.Items, TotalItems = result.TotalCount };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            _error = "Không thể tải downtime.";
            await InvokeAsync(StateHasChanged);
            return new TableData<DowntimeListItem> { Items = [], TotalItems = 0 };
        }
    }

    private async Task LoadStatsAsync()
    {
        // Chỉ lấy TotalCount với PageSize=1 cho từng KPI, không tải toàn bộ event vào browser.
        _totalCount = (await Query.DowntimeAsync(
            BuildQuery(1, 1, null, true, false, false),
            CancellationToken.None)).TotalCount;
        _unknownCount = (await Query.DowntimeAsync(
            BuildQuery(1, 1, null, true, true, false),
            CancellationToken.None)).TotalCount;
        _openCount = (await Query.DowntimeAsync(
            BuildQuery(1, 1, null, true, false, true),
            CancellationToken.None)).TotalCount;
    }

    private void ToggleSelection(DowntimeListItem item, bool selected)
    {
        if (selected)
        {
            _selected[item.Id] = item;
        }
        else
        {
            _selected.Remove(item.Id);
        }
    }

    private async Task BulkReclassifyAsync()
    {
        if (_selected.Count == 0) return;
        var parameters = new DialogParameters<DowntimeReclassifyDialog>
        {
            { x => x.Items, _selected.Values.ToArray() },
            { x => x.Reasons, AvailableBulkReasons.ToArray() }
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, BackdropClick = false, CloseOnEscapeKey = false };
        var dialog = await DialogService.ShowAsync<DowntimeReclassifyDialog>("Tái phân loại downtime", parameters, options);
        if (await dialog.Result is { Canceled: false })
        {
            _selected.Clear();
            await ReloadAllAsync();
        }
    }

    private async Task ReloadAllAsync()
    {
        await LoadStatsAsync();
        if (_table is not null)
        {
            await _table.ReloadAsync();
        }
    }

    private async Task ApplyFiltersAsync()
    {
        await LoadStatsAsync();
        if (_table is not null)
        {
            await _table.GoToFirstPageAndReloadAsync();
        }
    }

    private static Guid? ParseId(string? value) => Guid.TryParse(value, out Guid id) ? id : null;

    private static DateTimeOffset? StartOfDay(DateTime? value)
    {
        return value.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Local))
            : null;
    }

    private static DateTimeOffset? EndOfDay(DateTime? value)
    {
        return value.HasValue
            ? new DateTimeOffset(DateTime.SpecifyKind(value.Value.Date.AddDays(1), DateTimeKind.Local))
            : null;
    }
    private Task OpenCreateDialogAsync() => OpenDetailDialogAsync(Guid.Empty);

    private async Task OpenDetailDialogAsync(Guid id)
    {
        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true, BackdropClick = false, CloseOnEscapeKey = false };
        var parameters = new DialogParameters<DowntimeEditorDialog> { { x => x.Id, id } };
        var dialog = await DialogService.ShowAsync<DowntimeEditorDialog>(id == Guid.Empty ? "Ghi nhận dừng máy" : "Chi tiết sự kiện dừng máy", parameters, options);
        if (await dialog.Result is { Canceled: false })
        {
            _selected.Clear();
            await ReloadAllAsync();
        }
    }
}
