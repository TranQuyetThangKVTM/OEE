using MudBlazor;
using Oee.Application.Contracts.Modules;
using Oee.Application.Production.Samples.Delete;
using Oee.Web.Components.Shared;

namespace Oee.Web.Components.Pages.Production;

public partial class SamplePlans
{
    private PageTable<SamplePlanListItem>? table;
    private string? search;
    private string? error;
    private int totalCount;
    private async Task<TableData<SamplePlanListItem>> Load(TableState state, CancellationToken ct)
    {
        // Query trả các aggregate count (dòng/thiết bị/ca/sản lượng), không tải navigation rồi đếm ở browser.
        try
        {
            error = null;
            var result = await Query.SamplesAsync(new SamplePlanListQuery(search, state.SortLabel, state.SortDirection == SortDirection.Descending, state.Page + 1, state.PageSize), ct);
            if (totalCount != result.TotalCount)
            {
                totalCount = result.TotalCount;
                await InvokeAsync(StateHasChanged);
            }

            return new TableData<SamplePlanListItem>
            {
                Items = result.Items,
                TotalItems = result.TotalCount
            };
        }
        catch (OperationCanceledException)when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            error = "Không thể tải kế hoạch mẫu.";
            await InvokeAsync(StateHasChanged);
            return new()
            {
                Items = [],
                TotalItems = 0
            };
        }
    }

    private async Task DeleteAsync(SamplePlanListItem item)
    {
        // Confirm chỉ thu quyết định; service/DB kiểm tra reference và optimistic concurrency.
        var parameters = new DialogParameters<ConfirmDialog>
        {
            {
                x => x.Title,
                "Xóa kế hoạch mẫu"
            },
            {
                x => x.Message,
                $"Xóa kế hoạch mẫu {item.Code}?"},
            {
                x => x.ConfirmText,
                "Xóa mẫu"
            },
        };
        var dialog = await DialogService.ShowAsync<ConfirmDialog>("Xác nhận", parameters);
        var result = await dialog.Result;
        if (result is null || result.Canceled)
            return;
        try
        {
            await Commands.ExecuteAsync(new DeleteSamplePlanCommand(item.Id, Convert.FromBase64String(item.RowVersion)), CancellationToken.None);
            await Reload();
        }
        catch (Exception)
        {
            error = "Không thể xóa mẫu; dữ liệu có thể đã thay đổi.";
        }
    }

    private async Task OpenCreateDialogAsync()
    {
        // Bảng cha reload chỉ khi dialog Ok, nghĩa cha + các dòng đã commit SQL thành công.
        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Large,
            FullWidth = true,
            CloseOnEscapeKey = true
        };
        var dialog = await DialogService.ShowAsync<SamplePlanCreateDialog>("Thêm kế hoạch mẫu", options);
        var result = await dialog.Result;
        if (result is not null && !result.Canceled)
            await Reload();
    }

    private Task ApplyFiltersAsync() => table?.GoToFirstPageAndReloadAsync() ?? Task.CompletedTask;
    private Task Reload() => table?.ReloadAsync() ?? Task.CompletedTask;
    private async Task CopyAsync(Guid sourceId)
    {
        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true, BackdropClick = false, CloseOnEscapeKey = false };
        var dialog = await DialogService.ShowAsync<SamplePlanEditorDialog>("Sao chép kế hoạch mẫu", new DialogParameters<SamplePlanEditorDialog> { { x => x.CopyFrom, sourceId } }, options);
        if (await dialog.Result is { Canceled: false }) await Reload();
    }
}
