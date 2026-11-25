using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.Operations;
using Oee.Application.Downtime.Events.BulkReclassify;
using Oee.Application.Security;

namespace Oee.Web.Components.Pages.Production;

public partial class DowntimeReclassifyDialog
{
    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = default!;
    [Parameter] public IReadOnlyCollection<DowntimeListItem> Items { get; set; } = [];
    [Parameter] public IReadOnlyCollection<ProductionLookupOption> Reasons { get; set; } = [];
    private Guid? _reasonId;
    private bool _saving;
    private string? _error;

    private async Task SaveAsync()
    {
        if (_saving || !_reasonId.HasValue || Items.Count == 0) return;
        _saving = true;
        try
        {
            var authentication = await Authentication.GetAuthenticationStateAsync();
            if (!(await Authorization.AuthorizeAsync(authentication.User, Capabilities.DowntimeWrite)).Succeeded)
            {
                _error = "Bạn không có quyền tái phân loại sự kiện dừng máy.";
                return;
            }
            var items = Items.Select(x => new BulkReclassifyDowntimeItem(x.Id, x.ConcurrencyToken)).ToArray();
            await Commands.ExecuteAsync(new BulkReclassifyDowntimeEventCommand(items, _reasonId.Value), CancellationToken.None);
            Dialog.Close(DialogResult.Ok(true));
        }
        catch (Exception) { _error = "Không thể tái phân loại; một sự kiện có thể đã thay đổi."; }
        finally { _saving = false; }
    }

    private void Cancel() { if (!_saving) Dialog.Cancel(); }
}
