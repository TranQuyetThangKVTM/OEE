namespace Oee.Application.Contracts.PlcIntegration;

/// <summary>Request cấu hình OPC UA; CredentialSecretReference là tham chiếu secret, không phải password rõ.</summary>
public sealed record OpcUaConnectionRequest(
    string Code,
    string Name,
    string EndpointUrl,
    string SecurityMode,
    string SecurityPolicy,
    string AuthenticationType,
    string? UserName,
    string? CredentialSecretReference,
    bool ClearCredentialSecret,
    string? CertificateReference,
    bool IsActive,
    string? RowVersion = null);
