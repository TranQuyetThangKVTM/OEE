using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Oee.Application.Catalog.Products.Create;
using Oee.Application.Catalog.Standards.Create;
using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.Operations;
using Oee.Application.Downtime.Reasons.Create;
using Oee.Application.Organizations.Areas.Create;
using Oee.Application.Organizations.Enterprises.Create;
using Oee.Application.Organizations.Shifts.Create;
using Oee.Application.Organizations.Sites.Create;
using Oee.Application.Organizations.WorkCenters.Create;
using Oee.Application.Organizations.WorkUnits.Create;
using Oee.Application.Security;

namespace Oee.Web.Components.Pages.Master;

public partial class MasterDataCreateDialog
{
    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = default!;
    [Parameter, EditorRequired] public MasterCreateKind Kind { get; set; }
    [Parameter] public Guid? ParentId { get; set; }
    [Parameter] public bool LockParent { get; set; }
    [Parameter] public Guid? CopyFrom { get; set; }
    private MudForm? _form;
    private MasterDataLookups? _lookups;
    private string? _code;
    private string? _name;
    private string _uom = "PCS";
    private string? _description;
    private string _status = "Active";
    private string? _sourceCode;
    private string? _copySourceLabel;
    private WorkUnitStandardListItem? _sourceStandard;
    private Guid? _parentId;
    private Guid? _relatedId;
    private MasterLookupOption? _areaOption;
    private decimal _speed = 1m;
    private DateTime? _effectiveFrom = DateTime.UtcNow.Date;
    private DateTime? _effectiveTo;
    private string? _approvedBy;
    private string _shiftType = "Morning";
    private TimeSpan? _startTime = TimeSpan.FromHours(6);
    private TimeSpan? _endTime = TimeSpan.FromHours(14);
    private string _category = "AvailabilityLoss";
    private string _reasonKind = "Breakdown";
    private string _timeZoneId = "SE Asia Standard Time";
    private string? _error;
    private bool _loading = true;
    private bool _saving;
    private bool _valid;
    private bool _loadFailed;

    private string Title => (CopyFrom.HasValue, Kind) switch
    {
        (true, MasterCreateKind.Product) => "Sao chép sản phẩm",
        (true, MasterCreateKind.WorkUnitStandard) => "Sao chép định mức tốc độ",
        (_, MasterCreateKind.Product) => "Thêm sản phẩm mới",
        (_, MasterCreateKind.WorkUnitStandard) => "Thêm định mức tốc độ",
        (_, MasterCreateKind.Shift) => "Thêm ca làm việc",
        (_, MasterCreateKind.DowntimeReason) => "Thêm lý do dừng máy",
        (_, MasterCreateKind.WorkUnit) => "Thêm thiết bị",
        (_, MasterCreateKind.WorkCenter) => "Thêm dây chuyền",
        (_, MasterCreateKind.Area) => "Thêm khu vực sản xuất",
        (_, MasterCreateKind.Enterprise) => "Thêm doanh nghiệp",
        (_, MasterCreateKind.Site) => "Thêm nhà máy",
        _ => "Thêm dữ liệu nền",
    };

    protected override async Task OnInitializedAsync()
    {
        // Một lookup projection dùng chung tránh N+1; lỗi giữ dialog mở với MudAlert.
        try
        {
            _lookups = await Query.LookupsAsync(CancellationToken.None);
            if (ParentId.HasValue)
            {
                var parents = Kind switch
                {
                    MasterCreateKind.Site => _lookups.Enterprises,
                    MasterCreateKind.Area => _lookups.Sites,
                    MasterCreateKind.WorkCenter => _lookups.Areas,
                    MasterCreateKind.WorkUnit => _lookups.WorkCenters,
                    _ => throw new InvalidOperationException("Loại dữ liệu không có cấp cha trong cây."),
                };
                var parent = parents.SingleOrDefault(x => x.Id == ParentId && x.Active)
                    ?? throw new InvalidOperationException("Cấp cha không còn hoạt động. Hãy làm mới cây.");
                _parentId = parent.Id;
                _areaOption = Kind == MasterCreateKind.WorkCenter ? parent : null;
            }
            if (CopyFrom.HasValue)
                await LoadCopySourceAsync(CopyFrom.Value);
        }
        catch (Exception exception) { _loadFailed = true; _error = "Không thể tải dữ liệu nguồn hoặc danh mục đang hoạt động. " + exception.Message; }
        finally { _loading = false; }
    }

    private async Task LoadCopySourceAsync(Guid sourceId)
    {
        switch (Kind)
        {
            case MasterCreateKind.Product:
                var product = await Query.ProductAsync(sourceId, CancellationToken.None);
                _sourceCode = _code = product.Code;
                _name = product.Name;
                _uom = product.DefaultUom;
                _description = product.Description;
                _status = product.Status;
                _copySourceLabel = $"{product.Code} — {product.Name}";
                break;
            case MasterCreateKind.WorkUnitStandard:
                _sourceStandard = await Query.StandardAsync(sourceId, CancellationToken.None);
                _parentId = _sourceStandard.WorkUnitId;
                _relatedId = _sourceStandard.ProductId;
                _speed = _sourceStandard.SpeedPerMinute;
                _uom = _sourceStandard.Uom;
                _effectiveFrom = _sourceStandard.EffectiveFrom.ToDateTime(TimeOnly.MinValue);
                _effectiveTo = _sourceStandard.EffectiveTo?.ToDateTime(TimeOnly.MinValue);
                _approvedBy = _sourceStandard.ApprovedBy;
                _copySourceLabel = $"{_sourceStandard.WorkUnit} × {_sourceStandard.Product}";
                break;
            default:
                throw new InvalidOperationException("Loại dữ liệu này chưa hỗ trợ sao chép.");
        }
    }

    private async Task SaveAsync()
    {
        // Guard + disabled + progress chống double-click; server constraint là lớp chống request đồng thời.
        if (_saving || _loading || _loadFailed) return;
        await (_form?.ValidateAsync() ?? Task.CompletedTask);
        if (!_valid) return;
        _saving = true;
        _error = null;
        try
        {
            // UI kiểm tra MasterDataWrite để phản hồi sớm; command vẫn kiểm tra capability phía server.
            var authentication = await AuthenticationStateProvider.GetAuthenticationStateAsync();
            if (!(await Authorization.AuthorizeAsync(authentication.User, Capabilities.MasterDataWrite)).Succeeded)
            {
                _error = "Bạn không có quyền thay đổi dữ liệu nền.";
                return;
            }
            if (CopyFrom.HasValue && Kind == MasterCreateKind.Product
                && string.Equals(_code?.Trim(), _sourceCode, StringComparison.OrdinalIgnoreCase))
            {
                _error = "Hãy nhập mã SKU mới cho bản sao.";
                return;
            }
            if (_sourceStandard is not null && _parentId == _sourceStandard.WorkUnitId && _relatedId == _sourceStandard.ProductId)
            {
                _error = "Hãy chọn thiết bị hoặc SKU khác cho bản sao. Để thêm phiên bản cho cùng thiết bị × SKU, dùng Thêm phiên bản.";
                return;
            }
            // Mỗi nhánh tạo request DTO cụ thể; Razor không inject DbContext và không truyền EF Entity.
            MasterMutationResult result = Kind switch
            {
                MasterCreateKind.Product => await Commands.ExecuteAsync(new CreateProductCommand(_code ?? string.Empty, _name ?? string.Empty, _uom, _description, _status), CancellationToken.None),
                MasterCreateKind.WorkUnitStandard => await Commands.ExecuteAsync(new CreateStandardCommand(_parentId ?? Guid.Empty, _relatedId ?? Guid.Empty, _speed, _uom,
                        DateOnly.FromDateTime((_effectiveFrom ?? DateTime.UtcNow).Date),
                        _effectiveTo.HasValue ? DateOnly.FromDateTime(_effectiveTo.Value) : null, _approvedBy), CancellationToken.None),
                // TimeOnly chỉ biểu diễn giờ ca; 22:00–06:00 hợp lệ và được resolver hiểu là qua nửa đêm.
                MasterCreateKind.Shift => await Commands.ExecuteAsync(new CreateShiftCommand(_parentId ?? Guid.Empty, _code ?? string.Empty, _name ?? string.Empty,
                        TimeOnly.FromTimeSpan(_startTime ?? TimeSpan.Zero), TimeOnly.FromTimeSpan(_endTime ?? TimeSpan.Zero), "Active"), CancellationToken.None),
                MasterCreateKind.DowntimeReason => await Commands.ExecuteAsync(new CreateDowntimeReasonCommand(_code ?? string.Empty, _name ?? string.Empty, _category, _reasonKind, _description, "Active"), CancellationToken.None),
                MasterCreateKind.WorkUnit => await Commands.ExecuteAsync(new CreateWorkUnitCommand(_parentId ?? Guid.Empty, _code ?? string.Empty, _name ?? string.Empty, "Active", _description), CancellationToken.None),
                MasterCreateKind.WorkCenter => await Commands.ExecuteAsync(new CreateWorkCenterCommand(_areaOption?.Id ?? Guid.Empty, _code ?? string.Empty, _name ?? string.Empty, "Active", _description), CancellationToken.None),
                MasterCreateKind.Area => await Commands.ExecuteAsync(new CreateAreaCommand(_parentId ?? Guid.Empty, _code ?? string.Empty, _name ?? string.Empty, "Active", _description), CancellationToken.None),
                MasterCreateKind.Enterprise => await Commands.ExecuteAsync(new CreateEnterpriseCommand(_code ?? string.Empty, _name ?? string.Empty, _description, "Active"), CancellationToken.None),
                MasterCreateKind.Site => await Commands.ExecuteAsync(new CreateSiteCommand(_parentId ?? Guid.Empty, _code ?? string.Empty, _name ?? string.Empty, _timeZoneId, "Active", _description), CancellationToken.None),
                _ => throw new InvalidOperationException("Loại dữ liệu nền không hợp lệ."),
            };
            Snackbar.Add($"Đã lưu {Title.ToLowerInvariant()}.", Severity.Success);
            Dialog.Close(DialogResult.Ok(result));
        }
        catch (Exception exception) { _error = "Không thể lưu dữ liệu; mã có thể trùng hoặc dữ liệu không hợp lệ. " + exception.Message; }
        finally { _saving = false; }
    }

    private void ApplyShiftType(string value)
    {
        // Preset chỉ điền giờ trong form; Custom giữ lựa chọn người dùng.
        _shiftType = value;
        (_startTime, _endTime) = value switch
        {
            "Morning" => (TimeSpan.FromHours(6), TimeSpan.FromHours(14)),
            "Afternoon" => (TimeSpan.FromHours(14), TimeSpan.FromHours(22)),
            "Night" => (TimeSpan.FromHours(22), TimeSpan.FromHours(6)),
            _ => (_startTime, _endTime),
        };
    }

    private Task<IEnumerable<string>> SearchTimeZonesAsync(string? value, CancellationToken ct) =>
        MasterDataLookupSearch.TimeZonesAsync(_lookups?.TimeZoneIds, value, ct);

    private Task<IEnumerable<MasterLookupOption>> SearchAreasAsync(string? value, CancellationToken ct)
    {
        // Chỉ Area Active và hỗ trợ tìm code/name/Site; không query riêng cho từng ký tự ở SQL tại component.
        ct.ThrowIfCancellationRequested();
        IEnumerable<MasterLookupOption> options = Active(_lookups?.Areas);
        if (!string.IsNullOrWhiteSpace(value))
        {
            var term = value.Trim();
            options = options.Where(x => x.Code.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (x.ParentName?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        return Task.FromResult(options.Take(20));
    }

    private static string AreaText(MasterLookupOption? option) => option is null
        ? string.Empty
        : $"{option.ParentName} / {option.Code} — {option.Name}";

    private static IEnumerable<MasterLookupOption> Active(IEnumerable<MasterLookupOption>? source) =>
        source?.Where(x => x.Active) ?? [];
    private void Cancel() => Dialog.Cancel();
}
