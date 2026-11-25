using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Oee.Application.Common;
using Oee.Application.PlcIntegration;
using Oee.Infrastructure.Integrations.OpcUa;
using Oee.Infrastructure.Persistence;

namespace Oee.Infrastructure.IntegrationTests;

/// <summary>
/// Unit/registration Test bảo vệ cấu hình Dependency Injection của Infrastructure và OPC UA adapter.
/// </summary>
public sealed class InfrastructureRegistrationTests
{
    [Fact]
    public void EmptyConnectionStringDoesNotRegisterDatabaseContext()
    {
        // Arrange: composition root chưa có connection string hợp lệ.
        var services = new ServiceCollection();

        services.AddOeeInfrastructure(string.Empty);

        // Assert: không đăng ký DbContext lỗi, nhưng các abstraction không cần SQL vẫn sẵn sàng cho UI kỹ thuật.
        Assert.DoesNotContain(services, descriptor => descriptor.ServiceType == typeof(OeeDbContext));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IUseCaseExecutor));
    }

    [Fact]
    public void DatabaseContextIsTransientForConcurrentBlazorComponents()
    {
        // Arrange & Act: đăng ký Infrastructure với cấu trúc connection string hợp lệ nhưng không mở kết nối.
        var services = new ServiceCollection();

        services.AddOeeInfrastructure("Server=(local);Database=OeeRegistrationTest;Integrated Security=True;TrustServerCertificate=True");

        // Assert: mỗi luồng/component nhận DbContext riêng, tránh dùng đồng thời một instance không thread-safe.
        ServiceDescriptor context = Assert.Single(services, descriptor =>
            descriptor.ServiceType == typeof(OeeDbContext));
        Assert.Equal(ServiceLifetime.Transient, context.Lifetime);
    }

    [Fact]
    public void FakeOpcUaAdapterCanBeSelectedForUatWithoutRegisteringSdkAdapter()
    {
        // Arrange & Act: bật fake adapter cho UAT/CI và resolve qua abstraction Application.
        var services = new ServiceCollection();

        services.AddOeeOpcUaIntegration(useFakeAdapter: true);

        using var provider = services.BuildServiceProvider();
        var adapter = provider.GetRequiredService<IOpcUaClientAdapter>();
        // Assert: abstraction và concrete fake cùng trỏ tới singleton được chọn.
        Assert.IsType<FakeOpcUaAdapter>(adapter);
        Assert.Same(adapter, provider.GetRequiredService<FakeOpcUaAdapter>());
    }

    [Fact]
    public void WorkerCompositionCanExcludeWebOnlySignInManager()
    {
        // Arrange & Act: Worker đăng ký Identity store nhưng không có HTTP authentication context.
        var services = new ServiceCollection();

        services.AddOeeInfrastructure(
            "Server=(local);Database=OeeRegistrationTest;Integrated Security=True;TrustServerCertificate=True",
            includeSignInManager: false);

        // Assert: loại SignInManager dành cho Web, vẫn giữ UserManager cho nghiệp vụ tài khoản nếu cần.
        Assert.DoesNotContain(services, descriptor =>
            descriptor.ServiceType == typeof(SignInManager<Oee.Infrastructure.Identity.ApplicationUser>));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(UserManager<Oee.Infrastructure.Identity.ApplicationUser>));
    }
}
