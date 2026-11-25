global using Oee.Tests.UseCases;
using System.Reflection;
using System.Runtime.ExceptionServices;
using Oee.Application;
using Oee.Application.Common;

namespace Oee.Tests.UseCases;
/// <summary>Giữ các test nhiều bước dễ đọc trong khi mỗi lời gọi thực thi handler mới.</summary>
internal sealed class ScenarioHandlerExecutor(IReadOnlyDictionary<Type, object?> dependencies) : IUseCaseExecutor
{
    private readonly Dictionary<Type, object> instances = [];
    private static readonly Dictionary<Type, Type> Handlers = typeof(ApplicationAssembly).Assembly.GetTypes().Where(type => type is { IsClass: true, IsAbstract: false }).SelectMany(type => type.GetInterfaces().Where(contract => contract.IsGenericType && (contract.GetGenericTypeDefinition() == typeof(IRequestHandler<, >) || contract.GetGenericTypeDefinition() == typeof(ICommandHandler<>))).Select(contract => (Request: contract.GetGenericArguments()[0], Handler: type))).ToDictionary(item => item.Request, item => item.Handler);
    public Task<TResult> ExecuteAsync<TResult>(IRequest<TResult> request, CancellationToken cancellationToken = default)
    {
        if (ScenarioOperation.TryInvoke(request, type => dependencies.GetValueOrDefault(type), out var value, out var synchronous, cancellationToken))
            return synchronous ? Task.FromResult((TResult)value!) : (Task<TResult>)value!;
        return (Task<TResult>)Invoke(request, cancellationToken);
    }

    public Task ExecuteAsync(ICommand command, CancellationToken cancellationToken = default)
    {
        if (ScenarioOperation.TryInvoke(command, type => dependencies.GetValueOrDefault(type), out var value, out _, cancellationToken))
            return (Task)value!;
        return (Task)Invoke(command, cancellationToken);
    }

    private object Invoke(object request, CancellationToken cancellationToken)
    {
        var handler = Resolve(Handlers[request.GetType()]);
        return ScenarioOperation.InvokeUnwrapped(handler.GetType().GetMethod("HandleAsync")!, handler, [request, cancellationToken])!;
    }

    private object Resolve(Type type)
    {
        if (type == typeof(IUseCaseExecutor))
            return this;
        if (dependencies.TryGetValue(type, out var dependency))
            return dependency!;
        if (instances.TryGetValue(type, out var existing))
            return existing;
        if (type.IsInterface || type.IsAbstract)
            throw new InvalidOperationException($"Test scenario has no dependency for {type}.");
        var constructor = type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single();
        var arguments = constructor.GetParameters().Select(parameter => dependencies.TryGetValue(parameter.ParameterType, out var value) ? value : parameter.HasDefaultValue ? parameter.DefaultValue : Resolve(parameter.ParameterType)).ToArray();
        var instance = constructor.Invoke(arguments);
        instances[type] = instance;
        return instance;
    }
}

/// <summary>Chỉ dùng cho component tests: chuyển command mới tới fake scenario đã đăng ký.</summary>
internal sealed class ScenarioUseCaseExecutor(Func<Type, object?> resolve) : IUseCaseExecutor
{
    public Task<TResult> ExecuteAsync<TResult>(IRequest<TResult> request, CancellationToken cancellationToken = default)
    {
        if (!ScenarioOperation.TryInvoke(request, resolve, out var value, out var synchronous, cancellationToken))
            throw new InvalidOperationException($"No fake scenario registered for {request.GetType().Name}.");
        return synchronous ? Task.FromResult((TResult)value!) : (Task<TResult>)value!;
    }

    public Task ExecuteAsync(ICommand command, CancellationToken cancellationToken = default)
    {
        if (!ScenarioOperation.TryInvoke(command, resolve, out var value, out _, cancellationToken))
            throw new InvalidOperationException($"No fake scenario registered for {command.GetType().Name}.");
        return (Task)value!;
    }
}

internal sealed record ScenarioOperation(Type Contract, string Name, string? [] Properties, bool Synchronous)
{
    internal static bool TryInvoke(object request, Func<Type, object?> resolve, out object? result, out bool synchronous, CancellationToken cancellationToken)
    {
        foreach (var operation in ScenarioOperations.All.GetValueOrDefault(request.GetType()) ?? [])
        {
            if (resolve(operation.Contract)is not { } scenario)
                continue;
            var method = operation.Contract.GetMethods().Single(method => method.Name == operation.Name && method.GetParameters().Length == operation.Properties.Length);
            var arguments = operation.Properties.Select(property => property is null ? (object)cancellationToken : property == "$self" ? request : request.GetType().GetProperty(property)!.GetValue(request)).ToArray();
            result = InvokeUnwrapped(method, scenario, arguments);
            synchronous = operation.Synchronous;
            return true;
        }

        result = null;
        synchronous = false;
        return false;
    }

    internal static object? InvokeUnwrapped(MethodInfo method, object target, object? [] arguments)
    {
        try
        {
            return method.Invoke(target, arguments);
        }
        catch (TargetInvocationException exception)when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }
}
