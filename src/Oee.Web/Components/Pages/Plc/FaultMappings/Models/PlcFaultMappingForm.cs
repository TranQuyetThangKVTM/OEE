
namespace Oee.Web.Components.Pages.Plc;

internal sealed class PlcFaultMappingForm
{
    // Model binding tạm; PlcFaultMappingRequest typed mới là contract gửi vào Application.
    public string WorkUnitId { get; set; } = string.Empty;
    public string FaultCode { get; set; } = string.Empty;
    public string ReasonId { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool Active { get; set; } = true;
    public string? RowVersion { get; set; }
}
