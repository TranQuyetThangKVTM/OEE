using Oee.Application.Contracts.PlcIntegration;
using static Oee.Application.PlcIntegration.PlcConfigurationValidation;

namespace Oee.Application.PlcIntegration.Configuration.Connections.Common;

/// <summary>Kiểm tra và chuẩn hóa đầu vào, không đọc hoặc ghi database.</summary>
internal static class PlcConnectionValidator
{
    internal static PlcConnectionValues ValidateConnection(OpcUaConnectionRequest request)
    {
        // Chuẩn hóa chuỗi và chỉ chấp nhận scheme/security/authentication mà backend hỗ trợ rõ ràng.
        var code = Required(request.Code, 32, "Code");
        var name = Required(request.Name, 200, "Name");
        var endpoint = Required(request.EndpointUrl, 500, "Endpoint");

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            || !uri.Scheme.Equals("opc.tcp", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Endpoint must be an absolute opc.tcp URI.");
        var mode = Required(request.SecurityMode, 64, "Security mode");

        if (mode is not ("None" or "Sign" or "SignAndEncrypt"))
            throw new ArgumentException("Security mode is invalid.");
        var auth = Required(request.AuthenticationType, 32, "Authentication type");

        if (auth is not ("Anonymous" or "UserName"))
            throw new ArgumentException("Authentication type is invalid.");
        var userName = Optional(request.UserName, 256, "Username");
        var secret = Optional(request.CredentialSecretReference, 500, "Credential secret reference");

        if (auth == "UserName" && string.IsNullOrWhiteSpace(userName))
            throw new ArgumentException("Username is required for UserName authentication.");

        return new(
            code,
            name,
            endpoint,
            mode,
            Required(request.SecurityPolicy, 200, "Security policy"),
            auth,
            userName,
            secret,
            request.ClearCredentialSecret,
            Optional(request.CertificateReference, 500, "Certificate reference"),
            request.IsActive);
    }
}
