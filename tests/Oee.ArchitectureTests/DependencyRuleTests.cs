using Oee.Application;
using Oee.Application.Contracts.Operations;
using Oee.Domain;
using Oee.Infrastructure.Integrations.OpcUa;
using Oee.Infrastructure.Persistence;

namespace Oee.ArchitectureTests;

/// <summary>Giữ Domain/Application độc lập framework; DTO và adapter nằm đúng trong bốn lớp.</summary>
public sealed class DependencyRuleTests
{
    [Fact]
    public void DomainEntitiesRequireFactoriesAndDoNotExposePublicSetters()
    {
        var entities = typeof(DomainAssembly).Assembly.GetExportedTypes()
            .Where(type => !type.IsAbstract && typeof(global::Oee.Domain.Common.Entity).IsAssignableFrom(type)).ToArray();
        Assert.NotEmpty(entities);
        foreach (var type in entities)
        {
            Assert.Empty(type.GetConstructors());
            Assert.Contains(type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static),
                method => method.Name == "Create" && method.ReturnType == type);
            Assert.All(type.GetProperties(), property => Assert.False(property.SetMethod?.IsPublic == true,
                $"{type.Name}.{property.Name} must be changed through Domain behavior."));
        }
        Assert.Same(typeof(DomainAssembly).Assembly, typeof(global::Oee.Domain.PlcIntegration.CounterDeltaCalculator).Assembly);
    }

    [Theory]
    [InlineData("Domain")]
    [InlineData("Application")]
    public void CoreLayersDoNotReferenceInfrastructureWebOrFrameworks(string layer)
    {
        var assembly = layer == "Domain" ? typeof(DomainAssembly).Assembly : typeof(ApplicationAssembly).Assembly;
        string[] forbidden = ["Oee.Infrastructure", "Oee.Web", "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore",
            "MudBlazor", "Opc.Ua", "OPCFoundation"];
        if (layer == "Domain") forbidden = [.. forbidden, "Oee.Application"];
        var violations = assembly.GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(reference => forbidden.Any(prefix => reference.Equals(prefix, StringComparison.Ordinal)
                || reference.StartsWith(prefix + ".", StringComparison.Ordinal)))
            .ToArray();
        Assert.Empty(violations);
    }

    [Fact]
    public void ContractsAndOpcUaBelongToTheirOwningLayers()
    {
        Assert.Same(typeof(ApplicationAssembly).Assembly, typeof(ProductionPlanResponse).Assembly);
        Assert.Same(typeof(OeeDbContext).Assembly, typeof(OpcUaSdkAdapter).Assembly);
    }

    [Fact]
    public void BusinessUseCasesAreImplementedInApplication()
    {
        var useCases = new[] { typeof(ApplicationAssembly).Assembly, typeof(OeeDbContext).Assembly }
            .SelectMany(assembly => assembly.GetTypes()).Where(type => type.IsClass && !type.IsAbstract
            && type.GetInterfaces().Any(contract => contract.IsGenericType
                && (contract.GetGenericTypeDefinition() == typeof(global::Oee.Application.Common.IRequestHandler<,>)
                    || contract.GetGenericTypeDefinition() == typeof(global::Oee.Application.Common.ICommandHandler<>)))).ToArray();
        Assert.NotEmpty(useCases);
        Assert.All(useCases, type => Assert.Same(typeof(ApplicationAssembly).Assembly, type.Assembly));
        Assert.All(useCases.SelectMany(type => type.GetConstructors()).SelectMany(ctor => ctor.GetParameters()),
            parameter => Assert.StartsWith("Oee.Application", parameter.ParameterType.Namespace ?? string.Empty, StringComparison.Ordinal));
        Assert.All(useCases, type => Assert.Single(type.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly)));
        Assert.DoesNotContain(typeof(ApplicationAssembly).Assembly.GetTypes(), type => type.Name is "MasterDataCommandService" or "ProductionPlanService" or "PlcConfigurationService");
    }

    [Fact]
    public void ApplicationPortsDoNotExposeQueryableOrPersistenceFrameworkTypes()
    {
        var methods = typeof(ApplicationAssembly).Assembly.GetExportedTypes().Where(type => type.IsInterface)
            .SelectMany(type => type.GetMethods());
        foreach (var method in methods)
        {
            Assert.False(typeof(IQueryable).IsAssignableFrom(method.ReturnType), method.ToString());
            var types = method.GetParameters().Select(parameter => parameter.ParameterType).Append(method.ReturnType);
            Assert.All(types, type => Assert.DoesNotContain("Microsoft.EntityFrameworkCore", type.ToString(), StringComparison.Ordinal));
        }
    }
}
