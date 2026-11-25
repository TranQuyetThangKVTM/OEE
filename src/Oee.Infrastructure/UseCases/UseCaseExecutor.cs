using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Oee.Application.Common;

namespace Oee.Infrastructure.UseCases;

/// <summary>Giải quyết handler có kiểu trong scope hiện tại, dùng chung transaction và persistence ports.</summary>
internal sealed class UseCaseExecutor(IServiceProvider services) : IUseCaseExecutor
{
    private static readonly ConcurrentDictionary<Type, Func<IServiceProvider, ICommand, CancellationToken, Task>> Commands = new();

    public Task<TResult> ExecuteAsync<TResult>(IRequest<TResult> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var invoke = Results<TResult>.Handlers.GetOrAdd(request.GetType(), static type =>
            typeof(UseCaseExecutor).GetMethod(nameof(ExecuteRequest), BindingFlags.Static | BindingFlags.NonPublic)!
                .MakeGenericMethod(type, typeof(TResult))
                .CreateDelegate<Func<IServiceProvider, IRequest<TResult>, CancellationToken, Task<TResult>>>());
        return invoke(services, request, cancellationToken);
    }

    public Task ExecuteAsync(ICommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        var invoke = Commands.GetOrAdd(command.GetType(), static type =>
            typeof(UseCaseExecutor).GetMethod(nameof(ExecuteCommand), BindingFlags.Static | BindingFlags.NonPublic)!
                .MakeGenericMethod(type).CreateDelegate<Func<IServiceProvider, ICommand, CancellationToken, Task>>());
        return invoke(services, command, cancellationToken);
    }

    private static Task<TResult> ExecuteRequest<TRequest, TResult>(IServiceProvider services, IRequest<TResult> request, CancellationToken cancellationToken)
        where TRequest : IRequest<TResult> => services.GetRequiredService<IRequestHandler<TRequest, TResult>>().HandleAsync((TRequest)request, cancellationToken);

    private static Task ExecuteCommand<TCommand>(IServiceProvider services, ICommand command, CancellationToken cancellationToken)
        where TCommand : ICommand => services.GetRequiredService<ICommandHandler<TCommand>>().HandleAsync((TCommand)command, cancellationToken);

    private static class Results<TResult>
    {
        internal static readonly ConcurrentDictionary<Type, Func<IServiceProvider, IRequest<TResult>, CancellationToken, Task<TResult>>> Handlers = new();
    }
}
