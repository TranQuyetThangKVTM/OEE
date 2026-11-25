using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Oee.Application.Common;

namespace Oee.Tests.UseCases;
internal static class ScenarioServiceCollectionExtensions
{
    internal static IServiceCollection AddScenarioRequests(this IServiceCollection services)
    {
        services.TryAddScoped<IUseCaseExecutor>(provider => new ScenarioUseCaseExecutor(provider.GetService));
        return services;
    }
}
