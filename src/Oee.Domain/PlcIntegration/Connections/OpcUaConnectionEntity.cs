using Oee.Domain.Common;

namespace Oee.Domain.PlcIntegration;

/// <summary>Cấu hình ít thay đổi của một OPC UA server/session.</summary>
/// <remarks>Chỉ lưu secret reference/certificate reference; không lưu password hoặc private key dạng rõ.</remarks>
public sealed class OpcUaConnectionEntity : SoftDeletableAuditedEntity
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string EndpointUrl { get; private set; } = string.Empty;
    public string SecurityMode { get; private set; } = string.Empty;
    public string SecurityPolicy { get; private set; } = string.Empty;
    public string AuthenticationType { get; private set; } = "Anonymous";
    public string? UserName { get; private set; }
    public string? CredentialSecretReference { get; private set; }
    public string? CertificateReference { get; private set; }
    public bool IsActive { get; private set; }

    private OpcUaConnectionEntity()
    {
    }

    public static OpcUaConnectionEntity Create(string code, string name, string endpointUrl, string securityMode, string securityPolicy,
        string authenticationType = "Anonymous", string? userName = null, string? credentialSecretReference = null,
        string? certificateReference = null, bool isActive = true)
    {
        var auth = ValidateAuthenticationType(authenticationType);
        var user = NormalizeUserName(auth, userName);
        var secret = DomainGuard.Optional(credentialSecretReference, 500);
        var effectiveSecret = auth == "Anonymous" ? null : secret;
        EnsureCredentialAvailable(auth, isActive, effectiveSecret);
        return new OpcUaConnectionEntity
        {
            Code = DomainGuard.Required(code, 32),
            Name = DomainGuard.Required(name, 200),
            EndpointUrl = ValidateEndpoint(endpointUrl),
            SecurityMode = ValidateSecurityMode(securityMode),
            SecurityPolicy = DomainGuard.Required(securityPolicy, 200),
            AuthenticationType = auth,
            UserName = user,
            CredentialSecretReference = effectiveSecret,
            CertificateReference = DomainGuard.Optional(certificateReference, 500),
            IsActive = isActive
        };
    }

    public void Update(string code, string name, string endpointUrl, string securityMode, string securityPolicy,
        string authenticationType, string? userName, string? credentialSecretReference, string? certificateReference,
        bool isActive, bool keepExistingSecret = true, bool clearCredentialSecret = false)
    {
        var normalizedCode = DomainGuard.Required(code, 32);
        var normalizedName = DomainGuard.Required(name, 200);
        var endpoint = ValidateEndpoint(endpointUrl);
        var mode = ValidateSecurityMode(securityMode);
        var policy = DomainGuard.Required(securityPolicy, 200);
        var auth = ValidateAuthenticationType(authenticationType);
        var user = NormalizeUserName(auth, userName);
        var secret = DomainGuard.Optional(credentialSecretReference, 500);
        var certificate = DomainGuard.Optional(certificateReference, 500);
        var effectiveSecret = auth == "Anonymous" || clearCredentialSecret ? null : keepExistingSecret && secret is null ? CredentialSecretReference : secret;
        EnsureCredentialAvailable(auth, isActive, effectiveSecret);
        Code = normalizedCode;
        Name = normalizedName;
        EndpointUrl = endpoint;
        SecurityMode = mode;
        SecurityPolicy = policy;
        AuthenticationType = auth;
        UserName = user;
        CredentialSecretReference = effectiveSecret;
        CertificateReference = certificate;
        IsActive = isActive;
    }

    public void Delete(DateTimeOffset at)
    {
        SoftDelete(at);
        IsActive = false;
    }

    private static string ValidateEndpoint(string endpointUrl)
    {
        var endpoint = DomainGuard.Required(endpointUrl, 500);
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || !uri.Scheme.Equals("opc.tcp", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Endpoint must be an absolute opc.tcp URI.", nameof(endpointUrl));
        return endpoint;
    }

    private static string ValidateSecurityMode(string securityMode)
    {
        var mode = DomainGuard.Required(securityMode, 64);
        if (mode is not ("None" or "Sign" or "SignAndEncrypt"))
            throw new ArgumentException("Security mode is invalid.", nameof(securityMode));
        return mode;
    }

    private static string ValidateAuthenticationType(string authenticationType)
    {
        var auth = DomainGuard.Required(authenticationType, 32);
        if (auth is not ("Anonymous" or "UserName"))
            throw new ArgumentException("Authentication type is invalid.", nameof(authenticationType));
        return auth;
    }

    private static string? NormalizeUserName(string authenticationType, string? userName)
    {
        var user = DomainGuard.Optional(userName, 256);
        if (authenticationType == "UserName" && user is null)
            throw new ArgumentException("Username is required for UserName authentication.");
        return authenticationType == "Anonymous" ? null : user;
    }

    private static void EnsureCredentialAvailable(string authenticationType, bool isActive, string? secretReference)
    {
        if (isActive && authenticationType == "UserName" && secretReference is null)
            throw new ArgumentException("An active UserName connection requires a credential secret reference.");
    }
}
