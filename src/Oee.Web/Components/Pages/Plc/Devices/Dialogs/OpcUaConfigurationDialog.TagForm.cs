
namespace Oee.Web.Components.Pages.Plc;

public partial class OpcUaConfigurationDialog
{
    /// <summary>
    /// Model của một hàng tag, giữ role/direction bất biến trong khi cho phép nhập NodeId và DataType.
    /// </summary>
    /// <param name = "role">Vai trò nghiệp vụ của tag như TotalCounter hoặc Oee.</param>
    /// <param name = "direction">Chiều Read hoặc Write.</param>
    /// <param name = "dataType">Kiểu dữ liệu đã chuẩn hóa, độc lập OPC UA SDK.</param>
    /// <param name = "required">Cho biết tag bắt buộc phải có NodeId trước khi lưu.</param>
    public sealed class TagForm(string role, string direction, string dataType, bool required = false)
    {
        public Guid? Id { get; set; }
        public string Role { get; } = role;
        public string Direction { get; } = direction;
        public string DataType { get; set; } = dataType;
        public bool Required { get; set; } = required;
        public string? NodeId { get; set; }
        public string? DisplayName { get; set; }
    }
}
