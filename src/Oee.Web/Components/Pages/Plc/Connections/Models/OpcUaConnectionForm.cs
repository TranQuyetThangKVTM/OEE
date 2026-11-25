
namespace Oee.Web.Components.Pages.Plc;

internal sealed class OpcUaConnectionForm
{
    // Model binding của editor; không phải entity và SecretReference không bao giờ được hydrate từ SQL.
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Endpoint { get; set; } = "opc.tcp://";
    public string SecurityMode { get; set; } = "None";
    public string SecurityPolicy { get; set; } = "None";
    public string AuthenticationType { get; set; } = "Anonymous";
    public string? UserName { get; set; }
    public string? SecretReference { get; set; }
    public bool ClearSecret { get; set; }
    public string? CertificateReference { get; set; }
    public bool Active { get; set; } = true;
    public string? RowVersion { get; set; }
}
