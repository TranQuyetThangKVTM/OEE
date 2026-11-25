using Microsoft.AspNetCore.Components;
using MudBlazor;
using Oee.Application.Contracts.Modules;
using Oee.Web.Components.Presentation;

namespace Oee.Web.Components.Pages.Alerts;

public partial class AlertDetailDialog
{
    // Dialog instance được MudBlazor cascade; Alert là DTO do trang Alerts truyền vào.
    [CascadingParameter]
    private IMudDialogInstance Dialog { get; set; } = default!;

    [Parameter, EditorRequired]
    public AlertListItem Alert { get; set; } = default!;

    private void Close() => Dialog.Close();
    private static SemanticKind StateKind(string value) => value == "Resolved" ? SemanticKind.Success : value == "Acknowledged" ? SemanticKind.Info : SemanticKind.Warning;
    private static SemanticKind SeverityKind(string value) => value == "Critical" ? SemanticKind.Error : SemanticKind.Warning;
}
