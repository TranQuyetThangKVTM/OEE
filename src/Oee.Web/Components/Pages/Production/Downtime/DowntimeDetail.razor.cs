using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Oee.Web.Components.Pages.Production;

/// <summary>URL tương thích; form nhập chỉ được render bởi MudDialog.</summary>
public partial class DowntimeDetail
{
    [Parameter] public Guid Id { get; set; }
    private Guid? _openedFor;

    protected override Task OnAfterRenderAsync(bool firstRender) => _openedFor == Id ? Task.CompletedTask : OpenDialogAsync();

    private async Task OpenDialogAsync()
    {
        _openedFor = Id;
        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true, BackdropClick = false, CloseOnEscapeKey = false };
        var dialog = await DialogService.ShowAsync<DowntimeEditorDialog>("Sự kiện dừng máy", new DialogParameters<DowntimeEditorDialog> { { x => x.Id, Id } }, options);
        var result = await dialog.Result;
        Navigation.NavigateTo("/downtime");
    }
}
