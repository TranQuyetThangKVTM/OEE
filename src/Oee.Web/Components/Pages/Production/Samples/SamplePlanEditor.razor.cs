using Microsoft.AspNetCore.Components;
using MudBlazor;
using Oee.Application.Contracts.Operations;

namespace Oee.Web.Components.Pages.Production;

/// <summary>URL tương thích; form nhập chỉ được render bởi MudDialog.</summary>
public partial class SamplePlanEditor
{
    [Parameter] public Guid Id { get; set; }
    [SupplyParameterFromQuery] public Guid? CopyFrom { get; set; }
    private (Guid, Guid?)? _openedFor;

    protected override Task OnAfterRenderAsync(bool firstRender) => _openedFor == (Id, CopyFrom) ? Task.CompletedTask : OpenDialogAsync();

    private async Task OpenDialogAsync()
    {
        _openedFor = (Id, CopyFrom);
        var options = new DialogOptions { MaxWidth = MaxWidth.Medium, FullWidth = true, BackdropClick = false, CloseOnEscapeKey = false };
        var dialog = await DialogService.ShowAsync<SamplePlanEditorDialog>("Kế hoạch mẫu", new DialogParameters<SamplePlanEditorDialog> { { x => x.Id, Id }, { x => x.CopyFrom, CopyFrom } }, options);
        var result = await dialog.Result;
        Navigation.NavigateTo(result?.Data is SamplePlanResponse saved ? $"/production/plan-templates/{saved.Id}" : "/production/plan-templates");
    }
}
