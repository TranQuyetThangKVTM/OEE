using MudBlazor;
using Oee.Application.Contracts.Modules;
using Oee.Application.Oee.Alerts.Acknowledge;
using Oee.Application.Oee.Alerts.Queries;
using Oee.Application.Oee.Alerts.Reevaluate;
using Oee.Application.Oee.Alerts.Resolve;
using Oee.Web.Components.Presentation;
using Oee.Web.Components.Shared;

namespace Oee.Web.Components.Pages.Alerts;

public partial class Alerts
{
    private PageTable<AlertListItem>? _table;
    private string? _search;
    private string? _stateFilter = "Open";
    private string? _severityFilter;
    private DateTime? _fromDate = DateTime.Today.AddDays(-14);
    private DateTime? _toDate = DateTime.Today;
    private string? _error;
    private int _totalCount;
    private IReadOnlyCollection<AlertListItem> _visible = [];

    private async Task<TableData<AlertListItem>> LoadAsync(
        TableState state,
        CancellationToken cancellationToken)
    {
        try
        {
            // TableState của MudTable được chuyển thành AlertListQuery; CancellationToken hủy query cũ khi paging nhanh.
            _error = null;
            PagedResult<AlertListItem> result = await Service.ExecuteAsync(new ListOeeAlertsQuery(new AlertListQuery(
                    _search,
                    _stateFilter,
                    _severityFilter,
                    ToDateOnly(_fromDate),
                    ToDateOnly(_toDate),
                    state.SortLabel,
                    state.SortDirection != SortDirection.Ascending,
                    state.Page + 1,
                    state.PageSize)), cancellationToken);
            _visible = result.Items;
            await SetCountAsync(result.TotalCount);
            return new TableData<AlertListItem> { Items = result.Items, TotalItems = result.TotalCount };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            _error = "Không thể tải cảnh báo.";
            await InvokeAsync(StateHasChanged);
            return new TableData<AlertListItem> { Items = [], TotalItems = 0 };
        }
    }

    private Task ApplyFiltersAsync() => _table?.GoToFirstPageAndReloadAsync() ?? Task.CompletedTask;

    private Task ReloadAsync() => _table?.ReloadAsync() ?? Task.CompletedTask;

    private async Task AcknowledgeAsync(AlertListItem item)
    {
        // Chỉ gọi command sau DialogResult xác nhận; token cũ sẽ bị backend từ chối thay vì ghi đè.
        if (!await ConfirmAsync(
            "Tiếp nhận cảnh báo",
            $"Xác nhận tiếp nhận {item.Metric} của {item.WorkUnit}?",
            "Tiếp nhận"))
        {
            return;
        }
        await ExecuteAsync(() => Service.ExecuteAsync(new AcknowledgeOeeAlertCommand(item.Id, new AlertMutationRequest(item.ConcurrencyToken)), CancellationToken.None));
    }

    private async Task ResolveAsync(AlertListItem item)
    {
        if (!await ConfirmAsync(
            "Giải quyết cảnh báo",
            $"Đánh dấu cảnh báo {item.Metric} của {item.WorkUnit} đã giải quyết?",
            "Giải quyết"))
        {
            return;
        }
        await ExecuteAsync(() => Service.ExecuteAsync(new ResolveOeeAlertCommand(item.Id, new AlertMutationRequest(item.ConcurrencyToken)), CancellationToken.None));
    }

    private Task ReevaluateAsync(AlertListItem item)
    {
        return ExecuteAsync(() => Service.ExecuteAsync(new ReevaluateOeeAlertCommand(item.Id, item.ConcurrencyToken), CancellationToken.None));
    }

    private async Task ReevaluateVisibleAsync()
    {
        // Chỉ thao tác các row đang thấy và dừng khi có lỗi để không báo thành công sai cho toàn batch.
        foreach (AlertListItem item in _visible.Where(alert => alert.State != "Resolved"))
        {
            try
            {
                await Service.ExecuteAsync(new ReevaluateOeeAlertCommand(item.Id, item.ConcurrencyToken), CancellationToken.None);
            }
            catch (Exception)
            {
                _error = "Một cảnh báo không thể đánh giá lại.";
                break;
            }
        }
        await ReloadAsync();
    }

    private async Task ExecuteAsync(Func<Task> action)
    {
        // Wrapper dùng chung: giữ lỗi trên trang và chỉ reload bảng sau khi command thành công.
        try
        {
            _error = null;
            await action();
            await ReloadAsync();
        }
        catch (Exception)
        {
            _error = "Thao tác thất bại; cảnh báo có thể đã được người khác cập nhật.";
        }
    }

    private async Task ViewAsync(AlertListItem item)
    {
        // Dialog nhận DTO read-only, không truyền EF Entity đang tracking.
        DialogParameters<AlertDetailDialog> parameters = new()
        {
            { dialog => dialog.Alert, item }
        };
        await DialogService.ShowAsync<AlertDetailDialog>("Chi tiết cảnh báo", parameters);
    }

    private async Task<bool> ConfirmAsync(string title, string message, string confirmText)
    {
        DialogParameters<ConfirmDialog> parameters = new()
        {
            { dialog => dialog.Title, title },
            { dialog => dialog.Message, message },
            { dialog => dialog.ConfirmText, confirmText },
            { dialog => dialog.ConfirmColor, Color.Primary }
        };
        IDialogReference dialog = await DialogService.ShowAsync<ConfirmDialog>("Xác nhận", parameters);
        DialogResult? result = await dialog.Result;
        return result is { Canceled: false };
    }

    private async Task SetCountAsync(int count)
    {
        if (_totalCount != count)
        {
            _totalCount = count;
            await InvokeAsync(StateHasChanged);
        }
    }

    private static DateOnly? ToDateOnly(DateTime? value)
    {
        return value.HasValue ? DateOnly.FromDateTime(value.Value) : null;
    }

    private static string StateText(string value)
    {
        return value switch
        {
            "Open" => "Đang mở",
            "Acknowledged" => "Đã tiếp nhận",
            "Resolved" => "Đã giải quyết",
            _ => value
        };
    }

    private static string SeverityText(string value) => value == "Critical" ? "Nghiêm trọng" : "Cảnh báo";

    private static string MetricText(string value)
    {
        return value switch
        {
            "Availability" => "Sẵn sàng",
            "Performance" => "Hiệu suất",
            "Quality" => "Chất lượng",
            _ => value
        };
    }

    private static SemanticKind StateKind(string value)
    {
        return value switch
        {
            "Resolved" => SemanticKind.Success,
            "Acknowledged" => SemanticKind.Info,
            _ => SemanticKind.Warning
        };
    }

    private static SemanticKind SeverityKind(string value)
    {
        return value == "Critical" ? SemanticKind.Error : SemanticKind.Warning;
    }
}
