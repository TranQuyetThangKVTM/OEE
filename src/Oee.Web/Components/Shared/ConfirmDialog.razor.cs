using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Oee.Web.Components.Shared;

public partial class ConfirmDialog
{
    // IMudDialogInstance được MudBlazor cấp qua CascadingParameter để đóng/cancel đúng dialog hiện tại.
    [CascadingParameter]
    private IMudDialogInstance Dialog { get; set; } = default!;

    [Parameter]
    public string Title { get; set; } = "Xác nhận";

    [Parameter]
    public string Message { get; set; } = "Bạn có chắc chắn?";

    [Parameter]
    public string ConfirmText { get; set; } = "Xác nhận";

    [Parameter]
    public Color ConfirmColor { get; set; } = Color.Error;

    private void Cancel()
    {
        Dialog.Cancel();
    }

    private void Confirm()
    {
        // DialogResult.Ok(true) giúp page phân biệt xác nhận với đóng/cancel trước khi gọi command.
        Dialog.Close(DialogResult.Ok(true));
    }
}
