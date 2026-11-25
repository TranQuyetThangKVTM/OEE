using Microsoft.Extensions.DependencyInjection;
using Oee.Application;
using Oee.Application.Common;

namespace Oee.Infrastructure.UseCases;

internal static class UseCaseRegistration
{
    internal static IServiceCollection AddApplicationUseCases(this IServiceCollection services)
    {
        services.AddScoped<IUseCaseExecutor, UseCaseExecutor>();
        foreach (var type in typeof(ApplicationAssembly).Assembly.GetTypes().Where(type => type is { IsClass: true, IsAbstract: false }))
        {
            if (typeof(IUseCaseContext).IsAssignableFrom(type)) services.AddScoped(type);
            foreach (var contract in type.GetInterfaces().Where(contract => contract.IsGenericType
                && (contract.GetGenericTypeDefinition() == typeof(IRequestHandler<,>) || contract.GetGenericTypeDefinition() == typeof(ICommandHandler<>))))
                services.AddScoped(contract, type);
        }

        return services;
    }
}
