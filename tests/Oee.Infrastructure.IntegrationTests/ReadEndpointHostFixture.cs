using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Oee.Infrastructure.IntegrationTests;

// Các test chỉ đọc route/policy dùng một host; giữ cookie của mỗi HTTP client riêng biệt.
[CollectionDefinition("Read endpoint host", DisableParallelization = true)]
public sealed class ReadEndpointHostGroup : ICollectionFixture<ReadEndpointHostFixture>;

public sealed class ReadEndpointHostFixture : IDisposable
{
    public WebApplicationFactory<Program> App { get; } = new WebApplicationFactory<Program>()
        .WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["BackgroundProcessing:Enabled"] = "false",
                    ["OpcUa:Enabled"] = "false",
                    ["OpcUaWorker:Adapter"] = "Fake",
                    ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=EndpointMetadataOnly;Integrated Security=true;TrustServerCertificate=true;Connect Timeout=1"
                }));
            builder.ConfigureServices(services =>
            {
                foreach (var registration in services.Where(item => item.ServiceType == typeof(IHostedService)
                    && item.ImplementationType?.Namespace?.StartsWith("Oee.", StringComparison.Ordinal) == true).ToArray())
                    services.Remove(registration);
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
            });
        });

    public HttpClient CreateClient() => App.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    public void Dispose() => App.Dispose();
}
