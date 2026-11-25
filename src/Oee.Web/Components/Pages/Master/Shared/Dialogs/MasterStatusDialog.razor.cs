using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor;
using Oee.Application.Catalog.Products.SetProductStatus;
using Oee.Application.Contracts.Operations;
using Oee.Application.Downtime.Reasons.SetDowntimeReasonStatus;
using Oee.Application.Organizations.Areas.SetAreaStatus;
using Oee.Application.Organizations.Enterprises.SetEnterpriseStatus;
using Oee.Application.Organizations.Shifts.SetShiftStatus;
using Oee.Application.Organizations.Sites.SetSiteStatus;
using Oee.Application.Organizations.WorkCenters.SetWorkCenterStatus;
using Oee.Application.Organizations.WorkUnits.SetWorkUnitStatus;
using Oee.Application.Security;

namespace Oee.Web.Components.Pages.Master;

public partial class MasterStatusDialog
{
    [CascadingParameter]
    private IMudDialogInstance Dialog { get; set; } = default!;

    [Parameter, EditorRequired]
    public MasterEditKind Kind { get; set; }

    [Parameter, EditorRequired]
    public Guid Id { get; set; }

    private string? _code;
    private string? _name;
    private string? _rowVersion;
    private string _currentStatus = "Active";
    private string _targetStatus = "Inactive";
    private string? _error;
    private bool _loading = true;
    private bool _saving;
    private string ActionTitle => _targetStatus == "Active" ? "Kích hoạt lại" : "Tạm ngưng hoạt động";

    protected override async Task OnInitializedAsync()
    {
        // Switch query theo Kind chỉ lấy DTO; status mục tiêu được suy ra sau khi đã tải dữ liệu mới nhất.
        try
        {
            switch (Kind)
            {
                case MasterEditKind.Product:
                    var product = await Query.ProductAsync(Id, CancellationToken.None);
                    Set(product.Code, product.Name, product.Status, product.RowVersion);
                    break;
                case MasterEditKind.Shift:
                    var shift = await Query.ShiftAsync(Id, CancellationToken.None);
                    Set(shift.Code, shift.Name, shift.Status, shift.RowVersion);
                    break;
                case MasterEditKind.DowntimeReason:
                    var reason = await Query.DowntimeReasonAsync(Id, CancellationToken.None);
                    Set(reason.Code, reason.Name, reason.Status, reason.RowVersion);
                    break;
                case MasterEditKind.WorkUnit:
                    var unit = await Query.WorkUnitAsync(Id, CancellationToken.None);
                    Set(unit.Code, unit.Name, unit.Status, unit.RowVersion);
                    break;
                case MasterEditKind.WorkCenter:
                    var group = await Query.WorkCenterAsync(Id, CancellationToken.None);
                    Set(group.Code, group.Name, group.Status, group.RowVersion);
                    break;
                case MasterEditKind.Area:
                    var area = await Query.AreaAsync(Id, CancellationToken.None);
                    Set(area.Code, area.Name, area.Status, area.RowVersion);
                    break;
                case MasterEditKind.Enterprise:
                    var enterprise = await Query.EnterpriseAsync(Id, CancellationToken.None);
                    Set(enterprise.Code, enterprise.Name, enterprise.Status, enterprise.RowVersion);
                    break;
                case MasterEditKind.Site:
                    var site = await Query.SiteAsync(Id, CancellationToken.None);
                    Set(site.Code, site.Name, site.Status, site.RowVersion);
                    break;
            }
        }
        catch (Exception exception)
        {
            _error = "Không thể tải trạng thái mới nhất. " + exception.Message;
        }
        finally
        {
            _loading = false;
        }
    }

    private void Set(string code, string name, string status, string rowVersion)
    {
        _code = code;
        _name = name;
        _currentStatus = status;
        _targetStatus = status == "Active" ? "Inactive" : "Active";
        _rowVersion = rowVersion;
    }

    private async Task ConfirmAsync()
    {
        // Guard chống double click; RowVersion cũ phát concurrency conflict thay vì ghi đè.
        if (_saving || string.IsNullOrWhiteSpace(_rowVersion))
            return;
        _saving = true;
        _error = null;
        try
        {
            var authentication = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            if (!(await Authorization.AuthorizeAsync(authentication.User, Capabilities.MasterDataWrite)).Succeeded)
            {
                _error = "Bạn không có quyền thay đổi trạng thái.";
                return;
            }

            // Command chứa trực tiếp Id, token và trạng thái cần đổi.
            MasterMutationResult result = Kind switch
            {
                MasterEditKind.Product => await Commands.ExecuteAsync(new SetProductStatusCommand(Id, _rowVersion, _targetStatus), CancellationToken.None),
                MasterEditKind.Shift => await Commands.ExecuteAsync(new SetShiftStatusCommand(Id, _rowVersion, _targetStatus), CancellationToken.None),
                MasterEditKind.DowntimeReason => await Commands.ExecuteAsync(new SetDowntimeReasonStatusCommand(Id, _rowVersion, _targetStatus), CancellationToken.None),
                MasterEditKind.WorkUnit => await Commands.ExecuteAsync(new SetWorkUnitStatusCommand(Id, _rowVersion, _targetStatus), CancellationToken.None),
                MasterEditKind.WorkCenter => await Commands.ExecuteAsync(new SetWorkCenterStatusCommand(Id, _rowVersion, _targetStatus), CancellationToken.None),
                MasterEditKind.Area => await Commands.ExecuteAsync(new SetAreaStatusCommand(Id, _rowVersion, _targetStatus), CancellationToken.None),
                MasterEditKind.Enterprise => await Commands.ExecuteAsync(new SetEnterpriseStatusCommand(Id, _rowVersion, _targetStatus), CancellationToken.None),
                MasterEditKind.Site => await Commands.ExecuteAsync(new SetSiteStatusCommand(Id, _rowVersion, _targetStatus), CancellationToken.None),
                _ => throw new InvalidOperationException("Loại dữ liệu không hỗ trợ thay đổi trạng thái."),
            };
            Snackbar.Add(_targetStatus == "Active" ? "Đã kích hoạt lại bản ghi." : "Đã tạm ngưng bản ghi.", Severity.Success);
            Dialog.Close(DialogResult.Ok(result));
        }
        catch (Oee.Application.Common.ConcurrencyConflictException)
        {
            _error = "Dữ liệu đã thay đổi. Hãy đóng dialog, tải lại và thử lại.";
        }
        catch (Exception exception)
        {
            _error = "Không thể thay đổi trạng thái. " + exception.Message;
        }
        finally
        {
            _saving = false;
        }
    }

    private void Cancel() => Dialog.Cancel();
}
