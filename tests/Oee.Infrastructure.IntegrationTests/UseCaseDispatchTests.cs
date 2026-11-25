using Microsoft.Extensions.DependencyInjection;
using Oee.Application.Common;
using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.PlcIntegration;
using Oee.Application.PlcIntegration;
using Oee.Application.PlcIntegration.Configuration.Overview.Queries;

namespace Oee.Infrastructure.IntegrationTests;

public sealed class UseCaseDispatchTests
{
    [Fact]
    public async Task CachedDispatchUsesTheCurrentScopeForEveryRequest()
    {
        using var provider = CreateServices().BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        Guid firstId;
        using (var first = provider.CreateScope())
        {
            var requests = first.ServiceProvider.GetRequiredService<IUseCaseExecutor>();
            firstId = await requests.ExecuteAsync(new ScopeQuery());
            Assert.Equal(firstId, await requests.ExecuteAsync(new ScopeQuery()));
            Assert.Equal(first.ServiceProvider.GetRequiredService<ScopeStamp>().Id, firstId);
        }
        using var second = provider.CreateScope();
        var next = await second.ServiceProvider.GetRequiredService<IUseCaseExecutor>().ExecuteAsync(new ScopeQuery());
        Assert.NotEqual(firstId, next);
    }

    [Fact]
    public async Task VoidDispatchPreservesCancellationAndOriginalExceptions()
    {
        using var provider = CreateServices().BuildServiceProvider();
        using var scope = provider.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var requests = scope.ServiceProvider.GetRequiredService<IUseCaseExecutor>();
        var stamp = scope.ServiceProvider.GetRequiredService<ScopeStamp>();
        await requests.ExecuteAsync(new StampCommand(), cancellation.Token);
        Assert.Equal(1, stamp.Calls);
        Assert.Equal(cancellation.Token, stamp.Token);
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => requests.ExecuteAsync(new StampCommand(true)));
        Assert.Equal("command", exception.ParamName);
        Assert.Equal(1, stamp.Calls);
    }

    [Fact]
    public async Task CancelledRequestDoesNotMutateState()
    {
        using var provider = CreateServices().BuildServiceProvider();
        using var scope = provider.CreateScope();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var requests = scope.ServiceProvider.GetRequiredService<IUseCaseExecutor>();
        await Assert.ThrowsAsync<OperationCanceledException>(() => requests.ExecuteAsync(new StampCommand(), cancellation.Token));
        Assert.Equal(0, scope.ServiceProvider.GetRequiredService<ScopeStamp>().Calls);
    }

    [Fact]
    public async Task ApplicationQueryResolvesItsReadPortThroughProductionRegistration()
    {
        var reads = new LookupStore();
        var services = CreateServices();
        services.AddSingleton<IPlcConfigurationReadStore>(reads);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var result = await scope.ServiceProvider.GetRequiredService<IUseCaseExecutor>().ExecuteAsync(new GetPlcLookupsQuery(), cancellation.Token);
        Assert.Same(reads.Result, result);
        Assert.Equal(cancellation.Token, reads.Token);
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOeeInfrastructure(null, includeSignInManager: false, includeIdentityStore: false);
        services.AddScoped<ScopeStamp>();
        services.AddScoped<IRequestHandler<ScopeQuery, Guid>, ScopeQueryHandler>();
        services.AddScoped<ICommandHandler<StampCommand>, StampCommandHandler>();
        return services;
    }

    private sealed record ScopeQuery : IQuery<Guid>;
    private sealed record StampCommand(bool Fail = false) : ICommand;

    private sealed class ScopeStamp
    {
        internal Guid Id { get; } = Guid.NewGuid();
        internal int Calls { get; set; }
        internal CancellationToken Token { get; set; }
    }

    private sealed class ScopeQueryHandler(ScopeStamp stamp) : IRequestHandler<ScopeQuery, Guid>
    {
        public Task<Guid> HandleAsync(ScopeQuery request, CancellationToken cancellationToken) => Task.FromResult(stamp.Id);
    }

    private sealed class StampCommandHandler(ScopeStamp stamp) : ICommandHandler<StampCommand>
    {
        public Task HandleAsync(StampCommand command, CancellationToken cancellationToken)
        {
            if (command.Fail) throw new ArgumentException("Rejected command.", nameof(command));
            cancellationToken.ThrowIfCancellationRequested();
            stamp.Calls++;
            stamp.Token = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class LookupStore : IPlcConfigurationReadStore
    {
        internal PlcConfigurationLookups Result { get; } = new([], [], [], []);
        internal CancellationToken Token { get; private set; }
        public Task<PlcConfigurationLookups> LookupsAsync(CancellationToken ct)
        {
            Token = ct;
            return Task.FromResult(Result);
        }
        public Task<PagedResult<OpcUaConnectionItem>> ConnectionsAsync(PlcListQuery query, CancellationToken ct) => throw new NotSupportedException();
        public Task<PagedResult<PlcDeviceMappingItem>> DeviceMappingsAsync(PlcListQuery query, CancellationToken ct) => throw new NotSupportedException();
        public Task<PlcDeviceConfiguration> DeviceConfigurationAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<PagedResult<PlcFaultMappingItem>> FaultMappingsAsync(PlcListQuery query, CancellationToken ct) => throw new NotSupportedException();
    }
}
