using MudBlazor;
using Oee.Application.Contracts.Audit;
using Oee.Web.Components.Presentation;
using Oee.Web.Components.Shared;

namespace Oee.Web.Components.Pages;

public partial class Audit
{
    // Catalog tập trung ánh xạ tên kỹ thuật sang nhãn tiếng Việt; fallback vẫn giữ tên kỹ thuật an toàn.
    private static readonly string[] EntityTypes = AuditPresentationCatalog.EntityNames.Keys
        .Where(type => type is not "Process" and not "WorkUnitGroup")
        .Order(StringComparer.CurrentCulture).ToArray();
    private static readonly string[] Actions = AuditPresentationCatalog.ActionNames.Keys.Order(StringComparer.CurrentCulture).ToArray();
    private PageTable<AuditLogResponse>? _table;
    private string? _user;
    private string? _entity;
    private string? _action;
    private string? _error;
    private DateRange _range = new();
    private int _totalCount;
    private int _retentionMonths = 18;

    // UI chỉ đọc retention đã validate từ options, không cho client tự gửi số tháng tùy ý.
    protected override void OnInitialized() => _retentionMonths = RetentionOptions.Value.Months;

    private async Task<TableData<AuditLogResponse>> LoadAsync(TableState state, CancellationToken ct)
    {
        // Search/filter/time/sort/page được chuyển thành AuditLogSearchRequest và thực hiện AsNoTracking ở SQL.
        try
        {
            _error = null;
            var result = await AuditService.SearchAsync(new AuditLogSearchRequest(
                _user, _entity, _action, StartOfDay(_range.Start), EndOfDay(_range.End), state.SortLabel,
                state.SortLabel is null || state.SortDirection == SortDirection.Descending,
                state.Page + 1, state.PageSize), ct);
            await InvokeAsync(() => _totalCount = result.TotalCount);
            return new() { Items = result.Items, TotalItems = result.TotalCount };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            _error = "Không thể tải nhật ký thao tác.";
            await InvokeAsync(StateHasChanged);
            return new() { Items = [], TotalItems = 0 };
        }
    }

    private async Task ShowDetailsAsync(Guid id)
    {
        // Dialog nhận Id rồi tải lại detail/snapshot; không query entity nghiệp vụ hiện tại để dựng lịch sử.
        var parameters = new DialogParameters<AuditDetailDialog>
        {
            { dialog => dialog.AuditLogId, id }, { dialog => dialog.AuditService, AuditService },
        };
        await DialogService.ShowAsync<AuditDetailDialog>("Chi tiết nhật ký", parameters,
            new DialogOptions { MaxWidth = MaxWidth.ExtraLarge, FullWidth = true, CloseButton = true });
    }

    private async Task RunRetentionAsync()
    {
        // RoleGuard chỉ điều khiển nút; MaintenanceService vẫn kiểm tra AuditRetentionManage server-side.
        var dialog = await DialogService.ShowAsync<AuditRetentionDialog>("Dọn log cũ",
            new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true });
        if (await dialog.Result is { Canceled: false }) await ReloadAsync();
    }

    private Task ApplyFiltersAsync() => _table?.GoToFirstPageAndReloadAsync() ?? Task.CompletedTask;
    private Task ReloadAsync() => _table?.ReloadAsync() ?? Task.CompletedTask;
    // MudDateRangePicker trả ngày local; chuẩn hóa ranh giới nửa mở [from, to+1 ngày) về UTC để query.
    private static DateTimeOffset? StartOfDay(DateTime? value) => value.HasValue
        ? new DateTimeOffset(DateTime.SpecifyKind(value.Value.Date, DateTimeKind.Local)).ToUniversalTime() : null;
    private static DateTimeOffset? EndOfDay(DateTime? value) => value.HasValue
        ? new DateTimeOffset(DateTime.SpecifyKind(value.Value.Date.AddDays(1), DateTimeKind.Local)).ToUniversalTime() : null;

    private static string EntityName(string entity) => AuditPresentationCatalog.EntityName(entity);
    private static string ActionName(string action) => AuditPresentationCatalog.ActionName(action);
    private static SemanticKind ActionKind(string action) => action switch
    {
        // Chỉ ánh xạ màu trình bày, không thay đổi action đã persist.
        "Added" or "UserCreated" or "Activate" or "UserUnlocked" => SemanticKind.Success,
        "Deleted" or "UserDisabled" or "RetentionCleanup" => SemanticKind.Error,
        "Deactivate" or "UserLocked" or "UserPasswordReset" or "EndEffectiveness" => SemanticKind.Warning,
        _ => SemanticKind.Info,
    };
}
