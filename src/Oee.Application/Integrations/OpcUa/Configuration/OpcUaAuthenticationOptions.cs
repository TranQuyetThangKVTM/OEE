namespace Oee.Application.PlcIntegration;

/// <summary>Anonymous hoặc UsernamePassword; không ghi password vào SQL/audit.</summary>
public sealed class OpcUaAuthenticationOptions
{
    public string Mode { get; init; } = "Anonymous";
    public string? Username
    {
        get; init;
    }
    public string? Password
    {
        get; init;
    }
}
