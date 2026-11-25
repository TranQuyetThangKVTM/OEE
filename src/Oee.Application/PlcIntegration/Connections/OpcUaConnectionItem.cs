namespace Oee.Application.Contracts.PlcIntegration;

/// <summary>Dòng connection an toàn: chỉ cho biết có secret, không trả giá trị secret.</summary>
public sealed record OpcUaConnectionItem(
    Guid Id,
    string Code,
    string Name,
    string EndpointUrl,
    string SecurityMode,
    string SecurityPolicy,
    string AuthenticationType,
    string? UserName,
    bool HasCredentialSecret,
    string? CertificateReference,
    bool IsActive,
    string? LastSessionState,
    DateTimeOffset? LastSessionAt,
    string RowVersion);
