using Microsoft.AspNetCore.Components;

namespace Oee.Web.Components.Pages.Plc;

public partial class TagConfigurationRow
{
    [Parameter] public Guid? ConnectionId { get; set; }
    private bool _testing;
    private string? _testedNode;
    private Oee.Application.PlcIntegration.Monitoring.OpcTagDirectRead? _test;

    private void NodeChanged(string? nodeId)
    {
        Tag.NodeId = nodeId;
        Tag.DisplayName = null;
        _test = null;
    }

    private async Task TestAsync()
    {
        if (_testing || !ConnectionId.HasValue || string.IsNullOrWhiteSpace(Tag.NodeId)) return;
        _testing = true;
        _testedNode = Tag.NodeId;
        try
        {
            _test = await Plc.ExecuteAsync(new Oee.Application.PlcIntegration.Configuration.Connections.Queries.TestOpcUaTagQuery(
                ConnectionId.Value, Tag.NodeId, Tag.Role), CancellationToken.None);
        }
        catch { _test = new(Tag.NodeId, "—", "—", false, "Không thể đọc tag.", null, DateTimeOffset.UtcNow); }
        finally { _testing = false; }
    }
    [Parameter, EditorRequired]
    // TagForm chứa role cố định và giá trị người dùng đang nhập; đây không phải OPC SDK type.
    public OpcUaConfigurationDialog.TagForm Tag { get; set; } = default!;

    [Parameter, EditorRequired]
    public IReadOnlyCollection<string> DataTypes { get; set; } = [];

    [Parameter]
    public bool CanBrowse { get; set; }

    [Parameter]
    // EventCallback báo cho dialog cha mở node browser và nhận NodeId được chọn.
    public EventCallback BrowseRequested { get; set; }

    private void ClearTag()
    {
        Tag.NodeId = null;
        Tag.DisplayName = null;
    }

    private string FieldLabel => string.IsNullOrWhiteSpace(Tag.DisplayName) ? DisplayRole(Tag.Role) : $"{DisplayRole(Tag.Role)} → {Tag.DisplayName}";

    private static string DisplayRole(string role) => role switch
    {
        "Running" => "Tag trạng thái chạy",
        "FaultCode" => "Tag mã lỗi",
        "TotalCounter" => "Tag bộ đếm tổng",
        "DefectCounter" => "Tag bộ đếm lỗi",
        "ProductCode" => "Tag mã sản phẩm",
        "Oee" => "Tag OEE",
        "Availability" => "Tag Availability — A",
        "Performance" => "Tag Performance — P",
        "Quality" => "Tag Quality — Q",
        "CurrentDowntime" => "Tag thời gian dừng máy",
        "GoodQuantity" => "Tag sản lượng đạt — Good",
        "TargetQuantity" => "Tag sản lượng mục tiêu — Target",
        "Mtbf" => "Tag MTBF",
        "Mttr" => "Tag MTTR",
        _ => role,
    };
    private static string Description(string role) => role switch
    {
        "Running" => "true = máy chạy · false = máy dừng. Bắt buộc.",
        "FaultCode" => "Mã lỗi khi dừng máy, dùng để ánh xạ lý do dừng.",
        "TotalCounter" => "Bộ đếm tổng sản lượng. Bắt buộc, tính theo chế độ bên dưới.",
        "DefectCounter" => "Số sản phẩm lỗi. Để trống nếu PLC không có tag riêng.",
        "ProductCode" => "SKU đang chạy. Để trống để dùng sản phẩm mặc định / kế hoạch.",
        "Oee" => "Hiệu suất thiết bị · đơn vị %.",
        "Availability" => "Hệ số sẵn sàng · đơn vị %.",
        "Performance" => "Hệ số hiệu năng · đơn vị %.",
        "Quality" => "Hệ số chất lượng · đơn vị %.",
        "CurrentDowntime" => "Thời gian dừng hiện tại · đơn vị phút.",
        "GoodQuantity" => "Sản lượng đạt của ca · đơn vị sản phẩm.",
        "TargetQuantity" => "Sản lượng mục tiêu của kế hoạch đang chạy.",
        "Mtbf" => "Thời gian trung bình giữa các sự cố · đơn vị phút.",
        "Mttr" => "Thời gian khắc phục sự cố trung bình · đơn vị phút.",
        _ => string.Empty,
    };
    private static string DataTypeLabel(string type)
    {
        // Chỉ đổi nhãn kỹ thuật sang cách gọi quen thuộc với kỹ sư PLC; giá trị contract không bị thay đổi.
        return type switch
        {
            "BooleanValue" => "Bool",
            "NumericDecimal" => "Decimal",
            "DoublePrecision" => "Double",
            "SinglePrecision" => "Float",
            "Text" => "String",
            "Signed32" => "Int32",
            "Unsigned32" => "UInt32",
            "Signed64" => "Int64",
            "Unsigned64" => "UInt64",
            _ => type
        };
    }
}
