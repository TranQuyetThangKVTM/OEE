using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Oee.Web.Components.Pages.Master;

public partial class MasterEditActions
{
    [Parameter, EditorRequired]
    public MasterEditKind Kind { get; set; }

    [Parameter, EditorRequired]
    public Guid Id { get; set; }

    [Parameter, EditorRequired]
    public string Status { get; set; } = "Active";

    [Parameter]
    public string EditLabel { get; set; } = "Cập nhật";

    [Parameter]
    public string ActionLabel { get; set; } = "Thao tác bản ghi";

    [Parameter]
    public EventCallback OnChanged { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }
    private string StatusTooltip => Status == "Active" ? "Tạm ngưng hoạt động" : "Kích hoạt lại";

    private async Task OpenEditAsync()
    {
        // Dialog Small phù hợp form master; không truyền row/entity đang hiển thị ngoài Kind + Id.
        var parameters = new DialogParameters<MasterDataEditDialog>
        {
            {
                x => x.Kind,
                Kind
            },
            {
                x => x.Id,
                Id
            }
        };
        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
            CloseOnEscapeKey = true
        };
        var dialog = await DialogService.ShowAsync<MasterDataEditDialog>(EditLabel, parameters, options);
        var result = await dialog.Result;
        if (result is not null && !result.Canceled)
            await OnChanged.InvokeAsync();
    }

    private async Task OpenStatusAsync()
    {
        // Tooltip/action động theo status hiện tại, nhưng dialog vẫn tải lại status trước khi command.
        var parameters = new DialogParameters<MasterStatusDialog>
        {
            {
                x => x.Kind,
                Kind
            },
            {
                x => x.Id,
                Id
            }
        };
        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.ExtraSmall,
            FullWidth = true,
            CloseOnEscapeKey = true
        };
        var dialog = await DialogService.ShowAsync<MasterStatusDialog>(StatusTooltip, parameters, options);
        var result = await dialog.Result;
        if (result is not null && !result.Canceled)
            await OnChanged.InvokeAsync();
    }
}
