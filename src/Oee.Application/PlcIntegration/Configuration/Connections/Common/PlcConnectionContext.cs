using Oee.Application.Common;
using Oee.Domain.PlcIntegration;

namespace Oee.Application.PlcIntegration.Configuration.Connections.Common;

/// <summary>Dependency và quy tắc dùng chung của các thao tác PlcConnection.</summary>
internal sealed class PlcConnectionContext(PlcConfigurationContext context) : IUseCaseContext
{
    internal PlcConfigurationContext Context => context;
    internal IPlcConfigurationStore store => context.Store;
    internal IClock clock => context.Clock;
    internal IOpcUaClientAdapter opcUa => context.OpcUa;

    internal static OpcUaEndpointOptions ToEndpoint(OpcUaConnectionEntity x)
        => new(
            x.Id,
            x.EndpointUrl,
            x.SecurityPolicy,
            x.SecurityMode,
            x.AuthenticationType,
            x.UserName,
            x.CredentialSecretReference,
            x.CertificateReference);

    internal async Task EnsureConnectionCodeAsync(
        string code,
        Guid? except,
        CancellationToken cancellationToken)
    {
        if (await store.AnyConnectionsAsync(x => x.Code == code && x.Id != except, cancellationToken))
            throw new InvalidOperationException("Connection code already exists.");
    }
}
