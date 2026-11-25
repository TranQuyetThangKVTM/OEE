
namespace Oee.Web.Components.Pages.Plc;

public partial class OpcUaConfigurationDialog
{
    /// <summary>
    /// Model tạm dùng để binding các trường mapping trong MudBlazor dialog.
    /// Đây không phải Domain entity và không được EF Core tracking.
    /// </summary>
    public sealed class DeviceForm
    {
        public string ConnectionId { get; set; } = string.Empty;
        public string WorkUnitId { get; set; } = string.Empty;
        public string ProductId { get; set; } = string.Empty;
        public string ExternalDeviceId { get; set; } = string.Empty;
        public string CounterMode { get; set; } = "Cumulative";
        public string FaultCodeFormat { get; set; } = "Auto";
        public bool ZeroNoFault { get; set; } = true;
        public int DebounceSamples { get; set; } = 2;
        public string? Note { get; set; }
        public bool Active { get; set; } = true;
        public string? RowVersion { get; set; }
        public List<TagForm> Tags { get; } = [];
    }
}
