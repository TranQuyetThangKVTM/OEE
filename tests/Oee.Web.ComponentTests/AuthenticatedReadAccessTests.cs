using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Oee.Application.Security;
using Oee.Web.Security;

namespace Oee.Web.ComponentTests;

public sealed class AuthenticatedReadAccessTests
{
    [Fact]
    public async Task SignedInAccountWithoutRolesCanUseEveryReadPolicy()
    {
        using var provider = Services();
        var user = new ClaimsPrincipal(new ClaimsIdentity([], "Test"));
        var checker = Checker(user);
        foreach (var capability in Capabilities.All.Where(Capabilities.IsReadOnly))
        {
            Assert.True((await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, null, capability)).Succeeded);
            Assert.True(checker.HasCapability(capability));
        }
    }

    [Fact]
    public async Task SignedInAccountWithoutRolesDoesNotGainMutationCapabilities()
    {
        using var provider = Services();
        var user = new ClaimsPrincipal(new ClaimsIdentity([], "Test"));
        var checker = Checker(user);
        foreach (var capability in Capabilities.All.Where(capability => !Capabilities.IsReadOnly(capability)))
        {
            Assert.False((await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, null, capability)).Succeeded);
            Assert.False(checker.HasCapability(capability));
        }
    }

    [Fact]
    public async Task AnonymousIdentityCannotReadOrWriteEvenWithAnAdminRoleClaim()
    {
        using var provider = Services();
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, RoleCapabilityMatrix.Admin)]));
        var checker = Checker(user);
        foreach (var capability in Capabilities.All)
        {
            Assert.False((await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, null, capability)).Succeeded);
            Assert.False(checker.HasCapability(capability));
        }
    }

    [Fact]
    public async Task AdministratorKeepsExistingMutationPermissions()
    {
        using var provider = Services();
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, RoleCapabilityMatrix.Admin)], "Test"));
        var checker = Checker(user);
        foreach (var capability in Capabilities.All)
        {
            Assert.True((await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(user, null, capability)).Succeeded);
            Assert.True(checker.HasCapability(capability));
        }
    }

    private static ServiceProvider Services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCapabilityAuthorization();
        return services.BuildServiceProvider();
    }

    private static HttpCapabilityChecker Checker(ClaimsPrincipal user) =>
        new(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = user } });
}
