using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Oee.Application.PlcIntegration;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;

namespace Oee.Infrastructure.Integrations.OpcUa;

/// <summary>
/// Adapter duy nhất chuyển contract OPC UA trung lập của Application sang type của OPC Foundation SDK.
/// </summary>
/// <remarks>
/// SDK chỉ nằm trong Infrastructure; Domain/Application dùng contract trung lập. Adapter không ghi SQL;
/// dịch vụ nền nhận sample đã chuẩn hóa rồi chuyển tiếp tới processor để xử lý nghiệp vụ.
/// </remarks>
public sealed partial class OpcUaSdkAdapter(ICredentialResolver credentials, IOptions<OpcUaClientOptions> options,
    ILogger<OpcUaSdkAdapter>? logger = null) : IOpcUaClientAdapter
{
    /// <summary>Tạo và mở một OPC UA client session theo endpoint/security/authentication đã cấu hình.</summary>
    /// <param name="endpoint">Cấu hình trung lập, trong đó credential chỉ là secret reference.</param>
    /// <param name="cancellationToken">Cho phép dừng thao tác connect khi Worker shutdown.</param>
    /// <returns>Session bọc SDK bằng interface <see cref="IOpcUaSession"/>.</returns>
    public async Task<IOpcUaSession> ConnectAsync(OpcUaEndpointOptions endpoint, CancellationToken cancellationToken)
    {
        var client = options.Value;
        if (!client.IsValid()) throw new InvalidOperationException("OpcUa configuration is invalid.");
        endpoint = client.ApplyTo(endpoint);
        // Cấu hình certificate store, timeout và policy bảo mật của OPC UA client.
        // Private key/secret không được đưa vào comment hoặc log.
        var configuration = CreateConfiguration(endpoint);
        await configuration.ValidateAsync(ApplicationType.Client, cancellationToken);
        if (!endpoint.SecurityMode.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
#pragma warning disable CS0618
            var instance = new ApplicationInstance(configuration);
#pragma warning restore CS0618
            if (!await instance.CheckApplicationInstanceCertificatesAsync(false, null, cancellationToken))
                throw new InvalidOperationException("OPC UA application certificate is invalid.");
        }
#pragma warning disable CS0618
        var selected = await CoreClientUtils.SelectEndpointAsync(configuration, endpoint.EndpointUrl, !endpoint.SecurityMode.Equals("None", StringComparison.OrdinalIgnoreCase), client.OperationTimeoutMs, cancellationToken);
#pragma warning restore CS0618
        var configured = new ConfiguredEndpoint(null, selected, EndpointConfiguration.Create(configuration));
        if (!endpoint.SecurityMode.Equals("None", StringComparison.OrdinalIgnoreCase)
            && (selected.SecurityMode.ToString() != endpoint.SecurityMode
                || selected.SecurityPolicyUri != SecurityPolicies.GetUri(endpoint.SecurityPolicy)))
            throw new InvalidOperationException("Discovered OPC UA endpoint does not match the requested security mode/policy.");
        IUserIdentity identity = new UserIdentity();
        if (endpoint.AuthenticationType.Equals("UserName", StringComparison.OrdinalIgnoreCase))
        {
            // Chỉ resolve mật khẩu ở thời điểm connect; database và DTO không chứa mật khẩu rõ.
            var password = client.Enabled ? client.Auth.Password
                : string.IsNullOrWhiteSpace(endpoint.CredentialSecretReference) ? null
                : await credentials.ResolveAsync(endpoint.CredentialSecretReference, cancellationToken);
            if (string.IsNullOrWhiteSpace(endpoint.UserName) || string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException("OPC UA username credential was not resolved.");
            identity = new UserIdentity(endpoint.UserName, Encoding.UTF8.GetBytes(password));
        }
#pragma warning disable CS0618
        var factory = new DefaultSessionFactory();
#pragma warning restore CS0618
        var session = await factory.CreateAsync(configuration, configured, false, client.ApplicationName, (uint)client.SessionTimeoutMs, identity, null, cancellationToken);
        session.KeepAliveInterval = client.KeepAliveIntervalMs;
        return new OpcUaSdkSession(session, client.PublishingIntervalMs, logger ?? NullLogger<OpcUaSdkAdapter>.Instance);
    }

    /// <summary>Tạo cấu hình SDK có thể kiểm chứng offline; không mở network/session.</summary>
    public ApplicationConfiguration CreateConfiguration(OpcUaEndpointOptions endpoint)
    {
        var client = options.Value;
        var store = Path.GetFullPath(client.CertificateStorePath, AppContext.BaseDirectory);
        return new ApplicationConfiguration
        {
            ApplicationName = client.ApplicationName, ApplicationUri = client.ApplicationUri, ProductUri = client.ProductUri,
            ApplicationType = ApplicationType.Client,
            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = new CertificateIdentifier { StoreType = "Directory", StorePath = endpoint.CertificateReference ?? Path.Combine(store, "own"), SubjectName = client.ApplicationCertificateSubject },
                TrustedPeerCertificates = new CertificateTrustList { StoreType = "Directory", StorePath = Path.Combine(store, "trusted") },
                TrustedIssuerCertificates = new CertificateTrustList { StoreType = "Directory", StorePath = Path.Combine(store, "issuers") },
                RejectedCertificateStore = new CertificateTrustList { StoreType = "Directory", StorePath = Path.Combine(store, "rejected") },
                AutoAcceptUntrustedCertificates = client.AutoAcceptUntrustedCertificates, RejectSHA1SignedCertificates = true,
            },
            TransportConfigurations = [],
            TransportQuotas = new TransportQuotas { OperationTimeout = client.OperationTimeoutMs },
            ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = client.SessionTimeoutMs },
        };
    }

    /// <summary>Bọc ISession của SDK và normalize browse/read/write/subscribe về Application DTO.</summary>
    /// <summary>SDK gửi một MonitoredItemNotification cho từng item, không gửi collection trong event này.</summary>
    public static OpcUaValueSample? DecodeNotification(OpcUaReadRequest request, IEncodeable notificationValue)
    {
        if (notificationValue is not MonitoredItemNotification notification || notification.Value is not { } value) return null;
        return new(request.NodeId, request.Role, value.Value, value.SourceTimestamp,
            StatusCode.IsGood(value.StatusCode), value.StatusCode.ToString());
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "OPC UA notification delivery failed; error type {ErrorType}")]
    internal static partial void LogNotificationFailure(ILogger logger, string errorType);
}
