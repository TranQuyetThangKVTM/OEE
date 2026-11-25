using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using MudBlazor;

namespace Oee.Web.Components.Shared;

public partial class PageTable<TItem>
{
    /// <summary>Delegate nhận TableState/CancellationToken và trả đúng một page dữ liệu.</summary>
    [Parameter, EditorRequired]
    public Func<TableState, CancellationToken, Task<TableData<TItem>>> ItemsProvider { get; set; } = default!;

    [Parameter, EditorRequired]
    public RenderFragment HeaderContent { get; set; } = default!;

    [Parameter, EditorRequired]
    public RenderFragment<TItem> RowTemplate { get; set; } = default!;

    [Parameter]
    public bool Loading { get; set; }

    [Parameter]
    public string? Height { get; set; }

    [Parameter]
    public bool FixedHeader { get; set; }

    [Parameter]
    public bool Striped { get; set; } = true;

    [Parameter]
    public Func<TItem, int, string>? RowClassFunc { get; set; }

    [Parameter]
    public TableGroupDefinition<TItem>? GroupBy { get; set; }

    [Parameter]
    public RenderFragment<TableGroupData<object, TItem>>? GroupHeaderTemplate { get; set; }

    [Parameter]
    public string? GroupHeaderClass { get; set; }

    [Parameter]
    public int GroupColumnSpan { get; set; } = 1;

    [Parameter]
    public RenderFragment? ColGroup { get; set; }

    [Parameter]
    public int RowsPerPage { get; set; } = 25;

    [Parameter]
    public int[] PageSizeOptions { get; set; } = [10, 25, 50, 100];

    [Parameter]
    public string EmptyTitle { get; set; } = "Không có dữ liệu";

    [Parameter]
    public string AriaLabel { get; set; } = "Bảng dữ liệu";

    private MudTable<TItem>? innerTable;
    private readonly string tableId = $"oee-table-{Guid.NewGuid():N}";
    [Parameter]
    public RenderFragment? ToolbarContent { get; set; }
    private RenderFragment? EffectiveColGroup => ColGroup ?? (GroupBy is null ? null : GroupedColumns);

    private void GroupedColumns(RenderTreeBuilder builder)
    {
        builder.OpenElement(0, "col");
        builder.AddAttribute(1, "style", "width:36px");
        builder.CloseElement();
        for (var index = 0; index < GroupColumnSpan; index++)
        {
            builder.OpenElement(2, "col");
            builder.CloseElement();
        }
    }
    private TableData<TItem> lastSuccessfulData = new()
    {
        Items = [],
        TotalItems = 0
    };
    // CancellationToken của MudTable được chuyển nguyên vẹn tới query để hủy request cũ khi filter/page đổi nhanh.
    // Nếu request cũ bị hủy, giữ lại page đã tải thành công gần nhất thay vì ghi đè bảng bằng kết quả rỗng.
    private async Task<TableData<TItem>> LoadAsync(TableState state, CancellationToken cancellationToken)
    {
        try
        {
            TableData<TItem> result = await ItemsProvider(state, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return lastSuccessfulData;
            }

            lastSuccessfulData = result;
            return result;
        }
        catch (OperationCanceledException)when (cancellationToken.IsCancellationRequested)
        {
            return lastSuccessfulData;
        }
    }

    /// <summary>Tải lại trang hiện tại, dùng sau mutation thành công để giữ page/sort.</summary>
    public Task ReloadAsync() => innerTable?.ReloadServerData() ?? Task.CompletedTask;
    public void ExpandAllGroups() => innerTable?.ExpandAllGroups();
    public void CollapseAllGroups() => innerTable?.CollapseAllGroups();
    /// <summary>Quay về trang đầu và tải lại, phù hợp khi filter thay đổi.</summary>
    public async Task GoToFirstPageAndReloadAsync()
    {
        if (innerTable is null)
            return;
        // NavigateTo tự kích hoạt ServerData. Chỉ gọi ReloadServerData khi bảng đã ở trang đầu
        // để tránh tạo hai request liên tiếp rồi request đầu bị cancel.
        if (innerTable.CurrentPage != 0)
        {
            innerTable.NavigateTo(0);
            return;
        }

        await innerTable.ReloadServerData();
    }
}
