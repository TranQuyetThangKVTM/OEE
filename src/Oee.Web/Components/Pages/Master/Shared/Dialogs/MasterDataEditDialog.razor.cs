using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Oee.Application.Catalog.Products.Update;
using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.Operations;
using Oee.Application.Downtime.Reasons.Update;
using Oee.Application.Organizations.Areas.Update;
using Oee.Application.Organizations.Enterprises.Update;
using Oee.Application.Organizations.Shifts.Update;
using Oee.Application.Organizations.Sites.Update;
using Oee.Application.Organizations.WorkCenters.Update;
using Oee.Application.Organizations.WorkUnits.Update;
using Oee.Application.Security;

namespace Oee.Web.Components.Pages.Master;

public partial class MasterDataEditDialog
{
    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = default!;
    [Parameter, EditorRequired] public MasterEditKind Kind { get; set; }
    [Parameter, EditorRequired] public Guid Id { get; set; }

    private static readonly string[] AllowedUoms = ["PCS", "UNIT", "KG", "G", "L", "ML", "BOX", "CASE"];
    private MudForm? _form;
    private MasterDataLookups? _lookups;
    private string? _code;
    private string? _name;
    private string? _description;
    private string? _parent;
    private string _uom = "PCS";
    private string _timeZone = "SE Asia Standard Time";
    private string _category = "AvailabilityLoss";
    private string _reasonKind = "Breakdown";
    private string _shiftType = "Custom";
    private TimeSpan? _startTime;
    private TimeSpan? _endTime;
    private string? _rowVersion;
    private string? _loadError;
    private string? _saveError;
    private bool _loading = true;
    private bool _saving;
    private bool _valid;

    private string Title => Kind switch { MasterEditKind.Product => "Chỉnh sửa sản phẩm", MasterEditKind.Shift => "Cập nhật ca làm việc", MasterEditKind.DowntimeReason => "Cập nhật lý do dừng máy", MasterEditKind.WorkUnit => "Cập nhật thiết bị", MasterEditKind.WorkCenter => "Cập nhật dây chuyền", MasterEditKind.Area => "Cập nhật khu vực sản xuất", MasterEditKind.Enterprise => "Cập nhật doanh nghiệp", MasterEditKind.Site => "Cập nhật nhà máy", _ => "Cập nhật dữ liệu nền" };
    private string CodeLabel => Kind switch { MasterEditKind.Product => "Mã sản phẩm", MasterEditKind.Shift => "Mã ca", MasterEditKind.DowntimeReason => "Mã lý do", MasterEditKind.WorkUnit => "Mã thiết bị", MasterEditKind.WorkCenter => "Mã dây chuyền", MasterEditKind.Area => "Mã khu vực sản xuất", MasterEditKind.Enterprise => "Mã doanh nghiệp", MasterEditKind.Site => "Mã nhà máy", _ => "Mã" };
    private string NameLabel => Kind switch { MasterEditKind.Product => "Tên sản phẩm", MasterEditKind.Shift => "Tên ca", MasterEditKind.DowntimeReason => "Tên lý do", MasterEditKind.WorkUnit => "Tên thiết bị", MasterEditKind.WorkCenter => "Tên dây chuyền", MasterEditKind.Area => "Tên khu vực sản xuất", MasterEditKind.Enterprise => "Tên doanh nghiệp", MasterEditKind.Site => "Tên nhà máy", _ => "Tên" };
    private string ParentLabel => Kind switch { MasterEditKind.Shift or MasterEditKind.Area => "Nhà máy", MasterEditKind.WorkUnit => "Dây chuyền / Work Center", MasterEditKind.WorkCenter => "Khu vực sản xuất", MasterEditKind.Site => "Doanh nghiệp", _ => "Phạm vi cha" };
    private IEnumerable<MasterLookupOption> ValidReasonKinds => (_lookups?.DowntimeKinds ?? []).Where(x => IsValidReasonKind(_category, x.Code));
    private bool CodeEditable => Kind is MasterEditKind.Enterprise or MasterEditKind.Site or MasterEditKind.Area
        or MasterEditKind.WorkCenter or MasterEditKind.WorkUnit;

    protected override async Task OnInitializedAsync()
    {
        // Tải lookup + DTO mới nhất thay vì tin row đang có trên table; NotFound có state riêng.
        try
        {
            _lookups = await Query.LookupsAsync(CancellationToken.None);
            await LoadAsync();
        }
        catch (KeyNotFoundException) { _loadError = "Bản ghi không còn tồn tại hoặc đã bị xóa."; }
        catch (Exception exception) { _loadError = "Không thể tải dữ liệu mới nhất. " + exception.Message; }
        finally { _loading = false; }
    }

    private async Task LoadAsync()
    {
        // Mỗi query trả projection AsNoTracking và RowVersion Base64; Set chỉ copy field editable/read-only vào form.
        switch (Kind)
        {
            case MasterEditKind.Product:
                var product = await Query.ProductAsync(Id, CancellationToken.None); Set(product.Code, product.Name, product.RowVersion); _uom = product.DefaultUom.ToUpperInvariant(); _description = product.Description; break;
            case MasterEditKind.Shift:
                var shift = await Query.ShiftAsync(Id, CancellationToken.None); Set(shift.Code, shift.Name, shift.RowVersion); _parent = shift.Site; _startTime = shift.StartTime.ToTimeSpan(); _endTime = shift.EndTime.ToTimeSpan(); _shiftType = ResolveShiftType(_startTime.Value, _endTime.Value); break;
            case MasterEditKind.DowntimeReason:
                var reason = await Query.DowntimeReasonAsync(Id, CancellationToken.None); Set(reason.Code, reason.Name, reason.RowVersion); _category = reason.Category; _reasonKind = reason.Kind; _description = reason.Description; break;
            case MasterEditKind.WorkUnit:
                var unit = await Query.WorkUnitAsync(Id, CancellationToken.None); Set(unit.Code, unit.Name, unit.RowVersion); _description = unit.Description; _parent = unit.WorkCenter; break;
            case MasterEditKind.WorkCenter:
                var group = await Query.WorkCenterAsync(Id, CancellationToken.None); Set(group.Code, group.Name, group.RowVersion); _description = group.Description; _parent = group.Area; break;
            case MasterEditKind.Area:
                var area = await Query.AreaAsync(Id, CancellationToken.None); Set(area.Code, area.Name, area.RowVersion); _description = area.Description; _parent = area.Site; break;
            case MasterEditKind.Enterprise:
                var enterprise = await Query.EnterpriseAsync(Id, CancellationToken.None); Set(enterprise.Code, enterprise.Name, enterprise.RowVersion); _description = enterprise.Description; break;
            case MasterEditKind.Site:
                var site = await Query.SiteAsync(Id, CancellationToken.None); Set(site.Code, site.Name, site.RowVersion); _description = site.Description; _parent = site.Enterprise; _timeZone = site.TimeZoneId; break;
        }
    }

    private void Set(string code, string name, string rowVersion) { _code = code; _name = name; _rowVersion = rowVersion; }

    private async Task SaveAsync()
    {
        // StartTime == EndTime bị chặn; End < Start vẫn hợp lệ cho ca qua nửa đêm.
        if (_saving) return;
        await (_form?.ValidateAsync() ?? Task.CompletedTask);
        if (!_valid || string.IsNullOrWhiteSpace(_rowVersion)) return;
        if (Kind == MasterEditKind.Shift && (!_startTime.HasValue || !_endTime.HasValue || _startTime == _endTime)) { _saveError = "Giờ bắt đầu phải khác giờ kết thúc."; return; }
        _saving = true; _saveError = null;
        try
        {
            var authentication = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            if (!(await Authorization.AuthorizeAsync(authentication.User, Capabilities.MasterDataWrite)).Succeeded) { _saveError = "Bạn không có quyền cập nhật dữ liệu nền."; return; }
            // Update DTO chỉ gồm Id, field được phép và RowVersion: đây là chống over-posting phía contract.
            MasterMutationResult result = Kind switch
            {
                MasterEditKind.Product => await Commands.ExecuteAsync(new UpdateProductCommand(Id, _name ?? "", _uom, _description, _rowVersion), CancellationToken.None),
                MasterEditKind.Shift => await Commands.ExecuteAsync(new UpdateShiftCommand(Id, _name ?? "", TimeOnly.FromTimeSpan(_startTime!.Value), TimeOnly.FromTimeSpan(_endTime!.Value), _rowVersion), CancellationToken.None),
                MasterEditKind.DowntimeReason => await Commands.ExecuteAsync(new UpdateDowntimeReasonCommand(Id, _name ?? "", _category, _reasonKind, _description, _rowVersion), CancellationToken.None),
                MasterEditKind.WorkUnit => await Commands.ExecuteAsync(new UpdateWorkUnitCommand(Id, _name ?? "", _rowVersion, _description, _code), CancellationToken.None),
                MasterEditKind.WorkCenter => await Commands.ExecuteAsync(new UpdateWorkCenterCommand(Id, _name ?? "", _rowVersion, _description, _code), CancellationToken.None),
                MasterEditKind.Area => await Commands.ExecuteAsync(new UpdateAreaCommand(Id, _name ?? "", _rowVersion, _description, _code), CancellationToken.None),
                MasterEditKind.Enterprise => await Commands.ExecuteAsync(new UpdateEnterpriseCommand(Id, _name ?? "", _description, _rowVersion, _code), CancellationToken.None),
                MasterEditKind.Site => await Commands.ExecuteAsync(new UpdateSiteCommand(Id, _name ?? "", _timeZone, _rowVersion, _description, _code), CancellationToken.None),
                _ => throw new InvalidOperationException("Loại dữ liệu cập nhật không hợp lệ."),
            };
            Snackbar.Add($"Đã lưu {Title.ToLowerInvariant()}.", Severity.Success);
            Dialog.Close(DialogResult.Ok(result));
        }
        // Concurrency conflict giữ dialog mở để người dùng không hiểu nhầm dữ liệu đã được lưu.
        catch (Oee.Application.Common.ConcurrencyConflictException) { _saveError = "Dữ liệu đã được người khác cập nhật. Hãy đóng dialog, tải lại và thử lại."; }
        catch (Exception exception) { _saveError = "Không thể lưu dữ liệu. " + exception.Message; }
        finally { _saving = false; }
    }

    private void ApplyShiftType(string value) { _shiftType = value; (_startTime, _endTime) = value switch { "Morning" => (TimeSpan.FromHours(6), TimeSpan.FromHours(14)), "Afternoon" => (TimeSpan.FromHours(14), TimeSpan.FromHours(22)), "Night" => (TimeSpan.FromHours(22), TimeSpan.FromHours(6)), _ => (_startTime, _endTime) }; }
    private static string ResolveShiftType(TimeSpan start, TimeSpan end) => (start, end) switch { ({ Hours: 6, Minutes: 0 }, { Hours: 14, Minutes: 0 }) => "Morning", ({ Hours: 14, Minutes: 0 }, { Hours: 22, Minutes: 0 }) => "Afternoon", ({ Hours: 22, Minutes: 0 }, { Hours: 6, Minutes: 0 }) => "Night", _ => "Custom" };
    private Task<IEnumerable<string>> SearchTimeZonesAsync(string? value, CancellationToken ct) =>
        MasterDataLookupSearch.TimeZonesAsync(_lookups?.TimeZoneIds, value, ct);
    // UI chỉ lọc lựa chọn hợp lệ; Application vẫn validate lại tổ hợp Category + Kind.
    private static bool IsValidReasonKind(string category, string kind) => category switch { "Unknown" => kind == "Unknown", "AvailabilityLoss" => kind is "Planned" or "Failure" or "Breakdown" or "MinorStop", "PerformanceLoss" => kind == "MinorStop", "QualityLoss" => kind == "Quality", _ => false };
    private void Cancel() => Dialog.Cancel();
}
