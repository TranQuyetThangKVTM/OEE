using System.ComponentModel.DataAnnotations;

namespace Oee.Application.PlcIntegration;

/// <summary>Cấu hình client và endpoint dùng chung từ section OpcUa; khi tắt vẫn dùng endpoint theo mapping SQL.</summary>
public sealed class OpcUaClientOptions
{
    public const string SectionName = "OpcUa";
    public bool Enabled
    {
        get; init;
    }
    public string? EndpointUrl
    {
        get; init;
    }
    public bool UseSecurity
    {
        get; init;
    }
    public string SecurityMode { get; init; } = "None";
    public string SecurityPolicy { get; init; } = "None";
    public string ApplicationName { get; init; } = "OeeMasanNext.Web";
    public string ApplicationUri { get; init; } = "urn:OeeMasanNext:Web";
    public string ProductUri { get; init; } = "urn:OeeMasanNext";

    [Range(1000, 3600000)]
    public int SessionTimeoutMs { get; init; } = 60000;

    [Range(1000, 3600000)]
    public int KeepAliveIntervalMs { get; init; } = 10000;

    [Range(100, 60000)]
    public int PublishingIntervalMs { get; init; } = 250;

    [Range(1000, 3600000)]
    public int OperationTimeoutMs { get; init; } = 15000;

    [Range(1, 300)]
    public int ReconnectDelaySeconds { get; init; } = 2;
    public bool AutoAcceptUntrustedCertificates
    {
        get; init;
    }
    public string CertificateStorePath { get; init; } = "pki";
    public string ApplicationCertificateSubject { get; init; } = "CN=OeeMasanNext.Web";
    public OpcUaAuthenticationOptions Auth { get; init; } = new();

    /// <summary>Từ chối cấu hình không nhất quán trước khi kết nối server.</summary>
    public bool IsValid()
        => !Enabled
            || (Uri.TryCreate(EndpointUrl, UriKind.Absolute, out var uri) && uri.Scheme == "opc.tcp"
            && !string.IsNullOrWhiteSpace(ApplicationName)
            && Uri.TryCreate(ApplicationUri, UriKind.Absolute, out _)
            && Uri.TryCreate(ProductUri, UriKind.Absolute, out _)
            && !string.IsNullOrWhiteSpace(CertificateStorePath)
            && !string.IsNullOrWhiteSpace(ApplicationCertificateSubject)
            && (UseSecurity
            ? SecurityMode is "Sign" or "SignAndEncrypt" && SecurityPolicy != "None"
            : SecurityMode == "None" && SecurityPolicy == "None")
            && (Auth.Mode == "Anonymous"
            || Auth.Mode == "UsernamePassword" && !string.IsNullOrWhiteSpace(Auth.Username)
            && !string.IsNullOrWhiteSpace(Auth.Password)));

    /// <summary>Giữ ConnectionId để định tuyến mapping; endpoint chung ghi đè địa chỉ SQL khi Enabled.</summary>
    public OpcUaEndpointOptions ApplyTo(OpcUaEndpointOptions endpoint)
        => !Enabled
            ? endpoint
            : endpoint with
        {
            EndpointUrl = EndpointUrl!,
            SecurityMode = SecurityMode,
            SecurityPolicy = SecurityPolicy,
            AuthenticationType = Auth.Mode == "Anonymous" ? "Anonymous" : "UserName",
            UserName = Auth.Username,
            CredentialSecretReference = null,
            CertificateReference = null,
        };
}
