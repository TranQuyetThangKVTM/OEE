using Microsoft.AspNetCore.Components;
using MudBlazor;
using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.Operations;
using Oee.Application.Production.Samples.Delete;
using Oee.Web.Components.Shared;
using SampleDetailModel = Oee.Application.Contracts.Modules.SamplePlanDetail;

namespace Oee.Web.Components.Pages.Production;

public partial class SamplePlanDetail
{
    [Parameter] public Guid Id { get; set; }
    private SampleDetailModel? detail;
    private ProductionLookups? lookups;
    private PageTable<SamplePlanItemDetail>? table;
    private readonly HashSet<Guid> selected = [];
    private readonly Dictionary<Guid, SamplePlanItemDetail> visibleItems = [];
    private readonly TableGrouping<SamplePlanItemDetail> grouping = new(
        item => new(item.WorkUnitId, item.WorkUnit, item.WorkUnitName),
        item => new(item.ProductId, item.Product, item.ProductName), TableGroupingMode.Machine);
    private bool loading = true;
    private bool deleting;
    private string? error;
    private bool AllVisibleSelected => visibleItems.Count > 0 && selected.Count == visibleItems.Count;

    protected override Task OnParametersSetAsync()
    {
        if (detail?.Id != Id)
        {
            detail = null;
            selected.Clear();
            visibleItems.Clear();
        }
        return LoadHeaderAsync();
    }
    private async Task LoadHeaderAsync()
    {
        loading = detail is null;
        error = null;
        try
        {
            lookups = await Query.LookupsAsync(CancellationToken.None);
            detail = await Query.SampleAsync(Id, CancellationToken.None);
        }
        catch (Exception) { detail = null; error = "Không thể tải chi tiết mẫu. Hãy thử lại."; }
        finally { loading = false; }
    }

    private async Task<TableData<SamplePlanItemDetail>> LoadItems(TableState state, CancellationToken ct)
    {
        try
        {
            var result = await Query.SampleItemsAsync(Id, state.SortLabel, state.SortDirection == SortDirection.Descending,
                state.Page + 1, state.PageSize, ct);
            ct.ThrowIfCancellationRequested();
            selected.Clear();
            visibleItems.Clear();
            foreach (var item in result.Items)
            {
                visibleItems[item.Id] = item;
            }
            await InvokeAsync(StateHasChanged);
            return new() { Items = result.Items, TotalItems = result.TotalCount };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception)
        {
            selected.Clear();
            visibleItems.Clear();
            error = "Không thể tải các dòng mẫu. Hãy làm mới và thử lại.";
            await InvokeAsync(StateHasChanged);
            return new() { Items = [], TotalItems = 0 };
        }
    }

    private static string MachineRowClass(SamplePlanItemDetail item, int rowIndex) => "sample-machine-line";

    private async Task OpenAddItemsDialogAsync()
    {
        var parameters = new DialogParameters<SamplePlanItemsDialog> { { x => x.SamplePlanId, Id } };
        var options = new DialogOptions { MaxWidth = MaxWidth.Large, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<SamplePlanItemsDialog>("Thêm dòng vào mẫu", parameters, options);
        var result = await dialog.Result;
        if (result is not null && !result.Canceled) await RefreshAsync();
    }

    private async Task EditItemAsync(SamplePlanItemDetail item)
    {
        if (lookups is null) return;
        var parameters = new DialogParameters<SamplePlanItemEditDialog>
        {
            { x => x.Item, item }, { x => x.Lookups, lookups },
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true, BackdropClick = false };
        var dialog = await DialogService.ShowAsync<SamplePlanItemEditDialog>("Cập nhật dòng mẫu", parameters, options);
        var result = await dialog.Result;
        if (result is not null && !result.Canceled) await RefreshAsync();
    }

    private void Toggle(Guid id, bool value)
    {
        if (!visibleItems.ContainsKey(id)) return;
        if (value) selected.Add(id); else selected.Remove(id);
    }
    private void SelectVisible(bool value)
    {
        selected.Clear();
        if (value) selected.UnionWith(visibleItems.Keys);
    }

    private void ChangeGrouping(TableGroupingMode mode)
    {
        selected.Clear();
        grouping.SetMode(mode);
    }

    private void SelectGroup(IEnumerable<SamplePlanItemDetail> items, bool value)
    {
        foreach (var item in items)
            Toggle(item.Id, value);
    }
    private Task DeleteOneAsync(SamplePlanItemDetail item) => DeleteAsync([new(item.Id, item.RowVersion)]);
    private Task DeleteSelectedAsync() => DeleteAsync(selected.Where(visibleItems.ContainsKey)
        .Select(id => new SamplePlanItemConcurrency(id, visibleItems[id].RowVersion)).ToArray());

    private async Task DeleteAsync(SamplePlanItemConcurrency[] items)
    {
        if (deleting || items.Length == 0) return;
        deleting = true;
        try
        {
            var parameters = new DialogParameters<ConfirmDialog>
            {
                { x => x.Title, "Xóa dòng kế hoạch mẫu" },
                { x => x.Message, $"Xóa {items.Length} dòng đã chọn trong mẫu này?" },
                { x => x.ConfirmText, "Xóa" },
            };
            var options = new DialogOptions { MaxWidth = MaxWidth.ExtraSmall, FullWidth = true, CloseOnEscapeKey = true };
            var dialog = await DialogService.ShowAsync<ConfirmDialog>("Xóa dòng kế hoạch mẫu", parameters, options);
            var result = await dialog.Result;
            if (result is null || result.Canceled) return;
            await Commands.ExecuteAsync(new DeleteSamplePlanItemsCommand(items), CancellationToken.None);
            await RefreshAsync();
        }
        catch (Oee.Application.Common.ConcurrencyConflictException) { error = "Dòng mẫu đã thay đổi. Hãy làm mới và chọn lại các dòng cần xóa."; }
        catch (Exception) { error = "Không thể xóa các dòng mẫu. Hãy làm mới và thử lại."; }
        finally { deleting = false; }
    }

    private async Task RefreshAsync()
    {
        await LoadHeaderAsync();
        if (table is not null && detail is not null) await table.ReloadAsync();
    }
    private async Task OpenHeaderDialogAsync(bool copy)
    {
        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true, BackdropClick = false, CloseOnEscapeKey = false };
        var dialog = await DialogService.ShowAsync<SamplePlanEditorDialog>("Kế hoạch mẫu", new DialogParameters<SamplePlanEditorDialog> { { x => x.Id, copy ? Guid.Empty : Id }, { x => x.CopyFrom, copy ? (Guid?)Id : null } }, options);
        if (await dialog.Result is { Canceled: false }) await RefreshAsync();
    }
}
