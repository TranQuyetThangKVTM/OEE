using Microsoft.Extensions.DependencyInjection;
using Oee.Application.PlcIntegration;

namespace Oee.Infrastructure.Integrations.OpcUa;

/// <summary>
/// Đăng ký adapter OPC UA của Infrastructure vào Dependency Injection container của Web.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Chọn fake adapter cho Development/test hoặc SDK adapter cho kết nối OPC UA thật.</summary>
    /// <param name="services">Service collection của composition root.</param>
    /// <param name="useFakeAdapter">True để không cần PLC thật; false để dùng OPC UA SDK.</param>
    /// <returns>Chính <paramref name="services"/> để composition root có thể tiếp tục chain đăng ký.</returns>
    public static IServiceCollection AddOeeOpcUaIntegration(this IServiceCollection services, bool useFakeAdapter = false)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddOptions<OpcUaClientOptions>();
        // Resolver chỉ nhận tên biến môi trường (secret reference); cấu hình/database không cần giữ mật khẩu rõ.
        services.AddSingleton<ICredentialResolver, EnvironmentCredentialResolver>();
        if (useFakeAdapter)
        {
            services.AddSingleton<FakeOpcUaAdapter>();
            services.AddSingleton<IOpcUaClientAdapter>(provider => provider.GetRequiredService<FakeOpcUaAdapter>());
        }
        else
        {
            services.AddSingleton<IOpcUaClientAdapter, OpcUaSdkAdapter>();
        }

        return services;
    }
}
