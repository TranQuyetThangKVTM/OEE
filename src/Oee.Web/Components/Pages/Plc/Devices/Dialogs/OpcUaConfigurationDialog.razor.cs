using Microsoft.AspNetCore.Components;
using System.Globalization;
using MudBlazor;
using Oee.Application.Contracts.PlcIntegration;
using Oee.Application.PlcIntegration.Configuration.Connections.TestConnection;
using Oee.Application.PlcIntegration.Configuration.Devices.Create;
using Oee.Application.PlcIntegration.Configuration.Devices.Queries;
using Oee.Application.PlcIntegration.Configuration.Devices.Update;

namespace Oee.Web.Components.Pages.Plc;

public partial class OpcUaConfigurationDialog
{
    // Đây là các kiểu dữ liệu chuẩn hóa trong contract nội bộ, không phải enum/type của OPC UA SDK.
    private static readonly string[] DataTypes =
    [
        "BooleanValue",
        "Signed32",
        "Unsigned32",
        "Signed64",
        "Unsigned64",
        "SinglePrecision",
        "DoublePrecision",
        "Text"
    ];

    private bool _loading;
    private bool _saving;
    private bool _testing;
    private bool _browsing;
    private string? _error;
    private string? _testMessage;
    private PlcDeviceMappingItem? _status;
    private bool _refreshingStatus;
    private DeviceForm _form = CreateNewForm();

    [CascadingParameter]
    private IMudDialogInstance Dialog { get; set; } = default!;

    [Parameter]
    public Guid? MappingId { get; set; }

    [Parameter, EditorRequired]
    public PlcConfigurationLookups Lookups { get; set; } = default!;

    [Parameter, EditorRequired]
    public IReadOnlyCollection<OpcUaConnectionItem> Connections { get; set; } = [];

    private int ConfiguredRead => _form.Tags.Count(tag =>
        tag.Direction == "Read" && !string.IsNullOrWhiteSpace(tag.NodeId));

    private int ConfiguredWrite => _form.Tags.Count(tag =>
        tag.Direction == "Write" && !string.IsNullOrWhiteSpace(tag.NodeId));

    private bool CanBrowse => Guid.TryParse(_form.ConnectionId, out _);
    private Guid? SelectedConnectionId => Guid.TryParse(_form.ConnectionId, out var id) ? id : null;
    private string DialogTitle => Guid.TryParse(_form.WorkUnitId, out Guid id) && Lookups.WorkUnits.FirstOrDefault(x => x.Id == id) is { } unit
        ? $"Cấu hình OPC UA — {unit.Code} · {unit.Name}" : "Cấu hình OPC UA";
    private TagForm GetTag(string role) => _form.Tags.Single(x => x.Role == role);

    // TitleContent được render bởi container của MudDialog, cần báo container khi đổi thiết bị.
    private void RefreshDialogTitle() => Dialog.StateHasChanged();

    // Chỉ dùng lookup đã tải để hiện metadata endpoint; credential tuyệt đối không nằm trong DTO này.
    private OpcUaConnectionItem? SelectedConnection => Guid.TryParse(_form.ConnectionId, out Guid connectionId)
        ? Connections.FirstOrDefault(connection => connection.Id == connectionId)
        : null;

    protected override async Task OnInitializedAsync()
    {
        // Khi MappingId null, dialog giữ form mặc định cho luồng Create; khi có Id thì tải lại dữ liệu thật.
        if (!MappingId.HasValue)
        {
            return;
        }

        try
        {
            _loading = true;
            // Tải mapping và tag theo Id, tránh tin vào snapshot của dòng bảng đã có thể cũ.
            PlcDeviceConfiguration configuration = await Plc.ExecuteAsync(new GetPlcDeviceConfigurationQuery(MappingId.Value), CancellationToken.None);
            ApplyConfiguration(configuration);
            RefreshDialogTitle();
        }
        catch (Exception exception)
        {
            _error = "Không thể tải cấu hình. " + exception.Message;
        }
        finally
        {
            _loading = false;
        }
    }

    private void ApplyConfiguration(PlcDeviceConfiguration configuration)
    {
        // Bắt đầu từ danh sách role chuẩn để các tag tùy chọn chưa cấu hình vẫn xuất hiện trên form.
        PlcDeviceMappingItem mapping = configuration.Mapping;
        _form = CreateNewForm();
        _form.ConnectionId = mapping.ConnectionId.ToString();
        _form.WorkUnitId = mapping.WorkUnitId.ToString();
        _form.ProductId = mapping.DefaultProductId?.ToString() ?? string.Empty;
        _form.ExternalDeviceId = mapping.ExternalDeviceId;
        _form.CounterMode = mapping.CounterMode;
        _form.FaultCodeFormat = mapping.FaultCodeFormat;
        _form.ZeroNoFault = mapping.ZeroFaultCodeMeansNoFault;
        _form.DebounceSamples = mapping.DebounceSamples;
        _form.Note = mapping.Note;
        _form.Active = mapping.IsActive;
        _form.RowVersion = mapping.RowVersion;
        _status = mapping;

        foreach (PlcTagConfiguration savedTag in configuration.Tags)
        {
            // Ghép tag đã lưu bằng cặp Role + Direction; tag lạ từ phiên bản khác được bỏ qua an toàn trên UI.
            TagForm? tag = _form.Tags.FirstOrDefault(candidate =>
                candidate.Role == savedTag.Role && candidate.Direction == savedTag.Direction);
            if (tag is null)
            {
                continue;
            }

            tag.Id = savedTag.Id;
            tag.NodeId = savedTag.NodeId;
            tag.DataType = savedTag.DataType;
            tag.Required = savedTag.IsRequired;
        }
    }

    private async Task BrowseAsync(TagForm tag)
    {
        // Chỉ có thể browse sau khi người dùng chọn một connection hợp lệ.
        if (_browsing || _saving || !Guid.TryParse(_form.ConnectionId, out Guid connectionId))
        {
            return;
        }

        _browsing = true;
        try
        {
            DialogParameters<OpcUaNodeBrowserDialog> parameters = new()
            {
                { component => component.ConnectionId, connectionId },
                { component => component.InitialNodeId, string.IsNullOrWhiteSpace(tag.NodeId) ? null : tag.NodeId }
            };
            DialogOptions options = new() { FullWidth = true, MaxWidth = MaxWidth.Large };
            IDialogReference dialog = await DialogService.ShowAsync<OpcUaNodeBrowserDialog>(
                "Chọn tag từ namespace OPC UA", parameters, options);
            DialogResult? result = await dialog.Result;

            if (result is { Canceled: false, Data: OpcUaBrowseNode node })
            {
                // Chọn node chỉ cập nhật form tạm; SQL chưa thay đổi cho tới khi bấm LƯU.
                tag.NodeId = node.NodeId;
                tag.DisplayName = node.DisplayName;
                tag.DataType = NormalizeDataType(node.DataType) ?? tag.DataType;
            }
        }
        finally { _browsing = false; }
    }

    private static string? NormalizeDataType(string? value) => value switch
    {
        "Boolean" or "Bool" or "BooleanValue" => "BooleanValue",
        "Int32" or "Signed32" => "Signed32",
        "UInt32" or "Unsigned32" => "Unsigned32",
        "Int64" or "Signed64" => "Signed64",
        "UInt64" or "Unsigned64" => "Unsigned64",
        "Float" or "SinglePrecision" => "SinglePrecision",
        "Double" or "DoublePrecision" => "DoublePrecision",
        "String" or "Text" => "Text",
        _ => null,
    };

    private async Task TestConnectionAsync()
    {
        // Test dùng connection đã lưu và credential reference phía server; UI không nhận mật khẩu/certificate private key.
        if (SelectedConnection is null)
        {
            return;
        }

        try
        {
            _testing = true;
            _testMessage = null;
            OpcUaConnectionTestResult result = await Plc.ExecuteAsync(new TestOpcUaConnectionCommand(SelectedConnection.Id), CancellationToken.None);
            _error = result.Success ? null : result.Message;
            _testMessage = result.Success ? result.Message : null;
        }
        catch (Exception exception)
        {
            _error = "Không thể test connection. " + exception.Message;
        }
        finally
        {
            _testing = false;
        }
    }

    private async Task SaveAsync()
    {
        // _saving khóa nút để chống double-click; dialog chỉ đóng sau khi service hoàn tất thành công.
        try
        {
            _saving = true;
            _error = null;
            PlcDeviceConfigurationRequest request = CreateRequest();

            if (MappingId.HasValue)
            {
                // Request chứa RowVersion, vì vậy update lạc hậu sẽ bị Application/EF Core báo conflict.
                await Plc.ExecuteAsync(new UpdatePlcDeviceConfigurationCommand(MappingId.Value, request), CancellationToken.None);
            }
            else
            {
                // Create lưu mapping và toàn bộ tag hợp lệ trong workflow transaction của service.
                await Plc.ExecuteAsync(new CreatePlcDeviceConfigurationCommand(request), CancellationToken.None);
            }

            Dialog.Close(DialogResult.Ok(true));
        }
        catch (Exception exception)
        {
            _error = "Không thể lưu cấu hình; kiểm tra required tag hoặc dữ liệu đã thay đổi. "
                + exception.Message;
        }
        finally
        {
            _saving = false;
        }
    }

    private PlcDeviceConfigurationRequest CreateRequest()
    {
        // Kiểm tra hai khóa ngoại bắt buộc trước khi dựng request typed.
        if (!Guid.TryParse(_form.ConnectionId, out Guid connectionId)
            || !Guid.TryParse(_form.WorkUnitId, out Guid workUnitId))
        {
            throw new ArgumentException("Connection và WorkUnit là bắt buộc.");
        }

        PlcTagConfiguration[] tags = _form.Tags
            // Tag tùy chọn không có NodeId không được gửi; required tag thiếu sẽ bị backend từ chối.
            .Where(tag => !string.IsNullOrWhiteSpace(tag.NodeId))
            .Select(tag => new PlcTagConfiguration(
                tag.Id,
                tag.Role,
                tag.Direction,
                tag.NodeId!,
                tag.DataType,
                tag.Required))
            .ToArray();

        return new PlcDeviceConfigurationRequest(
            connectionId,
            workUnitId,
            Guid.TryParse(_form.ProductId, out Guid productId) ? productId : null,
            _form.ExternalDeviceId,
            _form.CounterMode,
            _form.FaultCodeFormat,
            _form.ZeroNoFault,
            _form.DebounceSamples,
            _form.Note,
            _form.Active,
            tags,
            _form.RowVersion);
    }

    private void Cancel()
    {
        Dialog.Cancel();
    }

    private async Task RefreshStatusAsync()
    {
        if (!MappingId.HasValue || _saving || _refreshingStatus) return;
        _refreshingStatus = true;
        try
        {
            // Chỉ làm mới trạng thái, không ghi đè form/tag hoặc RowVersion đang sửa.
            _status = (await Plc.ExecuteAsync(new GetPlcDeviceConfigurationQuery(MappingId.Value), CancellationToken.None)).Mapping;
        }
        catch { _error = "Không thể làm mới trạng thái OPC. Hãy thử lại."; }
        finally { _refreshingStatus = false; }
    }

    private static string StatusTime(DateTimeOffset? time) => time?.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture) ?? "Chưa có";

    private static DeviceForm CreateNewForm()
    {
        // Khởi tạo 5 role đọc và 9 role ghi theo contract; chỉ RunState/TotalCounter bắt buộc.
        DeviceForm form = new();
        form.Tags.AddRange(
        [
            new TagForm("Running", "Read", "BooleanValue", true),
            new TagForm("TotalCounter", "Read", "DoublePrecision", true),
            new TagForm("DefectCounter", "Read", "DoublePrecision"),
            new TagForm("FaultCode", "Read", "Text"),
            new TagForm("ProductCode", "Read", "Text"),
            new TagForm("Oee", "Write", "DoublePrecision"),
            new TagForm("Availability", "Write", "DoublePrecision"),
            new TagForm("Performance", "Write", "DoublePrecision"),
            new TagForm("Quality", "Write", "DoublePrecision"),
            new TagForm("CurrentDowntime", "Write", "DoublePrecision"),
            new TagForm("GoodQuantity", "Write", "DoublePrecision"),
            new TagForm("TargetQuantity", "Write", "DoublePrecision"),
            new TagForm("Mtbf", "Write", "DoublePrecision"),
            new TagForm("Mttr", "Write", "DoublePrecision")
        ]);
        return form;
    }
}
