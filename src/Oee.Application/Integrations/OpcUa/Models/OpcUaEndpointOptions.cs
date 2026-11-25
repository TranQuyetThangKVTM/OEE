namespace Oee.Application.PlcIntegration;

/// <summary>Cấu hình endpoint trung lập được Application chuyển cho adapter OPC UA.</summary>
/// <remarks>
/// CredentialSecretReference chỉ là tham chiếu đến secret store; mật khẩu rõ không thuộc DTO/domain
/// và không được log. SDK-specific SecurityPolicy type được giữ bên Integrations.OpcUa.
/// </remarks>
public sealed record OpcUaEndpointOptions(
    Guid ConnectionId,
    string EndpointUrl,
    string SecurityPolicy,
    string SecurityMode,
    string AuthenticationType,
    string? UserName,
    string? CredentialSecretReference,
    string? CertificateReference);
