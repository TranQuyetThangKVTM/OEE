using System.Globalization;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Oee.Application.Contracts.Modules;
using Oee.Application.Production.Plans.Delete;
using Oee.Domain.Common;
using Oee.Web.Components.Presentation;
using Oee.Web.Components.Shared;

namespace Oee.Web.Components.Pages.Production;

public partial class ProductionPlans
{
    private ProductionLookups? lookups;
    private PageTable<ProductionPlanListItem>? table;
    private readonly TableGrouping<ProductionPlanListItem> grouping = new(
        item => new(item.WorkUnitId, item.WorkUnit, item.WorkUnitName),
        item => new(item.ProductId, item.Product, item.ProductName));
    private string? search;
    private string? workUnitId;
    private string? productId;
    private DateTime? fromDate;
    private DateTime? untilDate;
    private string? error;
    private int totalCount;
    private int tableGeneration;
    private bool initializing = true;
    private string? lookupError;

    [Inject]
    private ILogger<ProductionPlans> Logger { get; set; } = default!;

    protected override Task OnInitializedAsync() => LoadLookupsAsync();

    private async Task LoadLookupsAsync()
    {
        initializing = true;
        lookupError = null;
        try
        {
            // Chờ lookup xong trước khi tạo bảng: cùng query service dùng một DbContext.
            lookups = await Query.LookupsAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            lookupError = "Không thể tải bộ lọc. Bấm Làm mới để thử lại.";
            LogLookupFailure(Logger, exception);
        }
        finally
        {
            initializing = false;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unable to load production plan filter options.")]
    private static partial void LogLookupFailure(ILogger logger, Exception exception);

    private async Task<TableData<ProductionPlanListItem>> Load(TableState state, CancellationToken ct)
    {
        // TableState chuyển thành query DTO; Skip/Take/sort chạy phía SQL, ct hủy request khi table reload.
        try
        {
            error = null;
            var result = await Query.PlansAsync(new ProductionPlanListQuery(search, Parse(workUnitId), Parse(productId),
                From(fromDate), Until(untilDate), state.SortLabel,
                state.SortLabel is null || state.SortDirection == SortDirection.Descending, state.Page + 1, state.PageSize), ct);
            if (totalCount != result.TotalCount) { totalCount = result.TotalCount; await InvokeAsync(StateHasChanged); }
            return new TableData<ProductionPlanListItem> { Items = result.Items, TotalItems = result.TotalCount };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            error = "Không thể tải kế hoạch.";
            await InvokeAsync(StateHasChanged);
            return new TableData<ProductionPlanListItem> { Items = [], TotalItems = 0 };
        }
    }

    private async Task EditAsync(ProductionPlanListItem item)
    {
        var parameters = new DialogParameters<ProductionPlanEditDialog> { { x => x.Id, item.Id } };
        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true, CloseOnEscapeKey = true, BackdropClick = false };
        var dialog = await DialogService.ShowAsync<ProductionPlanEditDialog>("Cập nhật kế hoạch", parameters, options);
        var result = await dialog.Result;
        if (result is not null && !result.Canceled) await Reload();
    }

    private async Task DeleteAsync(ProductionPlanListItem item)
    {
        var parameters = new DialogParameters<ConfirmDialog>
        {
            { x => x.Title, "Xóa kế hoạch" },
            { x => x.Message, $"Xóa kế hoạch {item.Code}?" },
            { x => x.ConfirmText, "Xóa" },
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.ExtraSmall, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<ConfirmDialog>("Xóa kế hoạch", parameters, options);
        var result = await dialog.Result;
        if (result is null || result.Canceled) return;
        try
        {
            await Commands.ExecuteAsync(new DeleteProductionPlanCommand(item.Id, Convert.FromBase64String(item.RowVersion)), CancellationToken.None);
            await Reload();
        }
        catch (Oee.Application.Common.ConcurrencyConflictException) { error = "Kế hoạch đã thay đổi. Hãy làm mới danh sách rồi thử lại."; }
        catch (Exception) { error = "Không thể xóa kế hoạch. Hãy làm mới danh sách rồi thử lại."; }
    }
    private async Task OpenCreateDialogAsync()
    {
        var options = new DialogOptions { MaxWidth = MaxWidth.Large, FullWidth = true, CloseOnEscapeKey = true, BackdropClick = false };
        var dialog = await DialogService.ShowAsync<ProductionPlanCreateDialog>("Thêm kế hoạch sản xuất", options);
        var result = await dialog.Result;
        // Chỉ reload bảng cha khi dialog đóng bằng DialogResult.Ok sau transaction thành công.
        if (result is not null && !result.Canceled) await Reload();
    }

    private async Task OpenFromSampleDialogAsync()
    {
        var options = new DialogOptions { MaxWidth = MaxWidth.Large, FullWidth = true, CloseOnEscapeKey = true, BackdropClick = false };
        var dialog = await DialogService.ShowAsync<ProductionPlanFromSampleDialog>("Tạo kế hoạch từ mẫu", options);
        var result = await dialog.Result;
        if (result is not null && !result.Canceled) await Reload();
    }

    private Task ApplyFiltersAsync() => table?.GoToFirstPageAndReloadAsync() ?? Task.CompletedTask;

    private async Task Reload()
    {
        if (initializing) return;
        if (lookups is null)
        {
            initializing = true;
            await InvokeAsync(StateHasChanged);
            await LoadLookupsAsync();
            tableGeneration++;
            // Bảng được tạo lại sau lookup và tự tải trang đầu, không phát thêm query trùng.
            await InvokeAsync(StateHasChanged);
            return;
        }
        if (table is not null) await table.ReloadAsync();
    }
    private static Guid? Parse(string? value) => Guid.TryParse(value, out var id) ? id : null;
    private static DateTimeOffset? From(DateTime? value) => value.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Local)) : null;
    private static DateTimeOffset? Until(DateTime? value) => value.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(value.Value.Date.AddDays(1), DateTimeKind.Local)) : null;
    private static string FormatRatio(decimal? value) => value.HasValue ? value.Value.ToString("P1", CultureInfo.CurrentCulture) : "—";

    private static (string Text, SemanticKind Kind) ExecutionPresentation(ProductionPlanExecutionStatusEnum status) => status switch
    {
        ProductionPlanExecutionStatusEnum.Pending => ("Chưa bắt đầu", SemanticKind.Neutral),
        ProductionPlanExecutionStatusEnum.InProgress => ("Đang thực hiện", SemanticKind.Info),
        ProductionPlanExecutionStatusEnum.MissingOutput => ("Chưa ghi nhận", SemanticKind.Warning),
        ProductionPlanExecutionStatusEnum.CompletedUnderTarget => ("Hụt KH", SemanticKind.Error),
        ProductionPlanExecutionStatusEnum.CompletedOnTarget => ("Đạt KH", SemanticKind.Success),
        _ => ("—", SemanticKind.Neutral)
    };

    private static Color AttainmentColor(ProductionPlanExecutionStatusEnum status) => status switch
    {
        ProductionPlanExecutionStatusEnum.CompletedOnTarget => Color.Success,
        ProductionPlanExecutionStatusEnum.CompletedUnderTarget => Color.Error,
        ProductionPlanExecutionStatusEnum.MissingOutput => Color.Warning,
        _ => Color.Default
    };

    private static string ShiftPeriod(ProductionPlanListItem item)
    {
        var start = item.Start.ToLocalTime();
        var end = item.End.ToLocalTime();
        return start.ToString("dd/MM/yyyy · HH:mm", CultureInfo.CurrentCulture) + " – " +
            end.ToString(start.Date == end.Date ? "HH:mm" : "dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);
    }
}
