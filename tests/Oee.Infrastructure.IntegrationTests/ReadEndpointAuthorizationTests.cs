using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Oee.Application.Security;

namespace Oee.Infrastructure.IntegrationTests;

[Collection("Read endpoint host")]
public sealed class ReadEndpointAuthorizationTests
{
    private readonly WebApplicationFactory<Program> app;

    public ReadEndpointAuthorizationTests(ReadEndpointHostFixture fixture)
    {
        app = fixture.App;
    }

    [Theory]
    [InlineData("/api/v1/production-plans")]
    [InlineData("/api/v1/production-plans/{id:guid}")]
    [InlineData("/api/v1/users")]
    [InlineData("/api/v1/users/{id:guid}")]
    [InlineData("/api/v1/users/roles")]
    [InlineData("/api/v1/plc/fault-mappings/import/template")]
    [InlineData("/production/plans")]
    [InlineData("/production/plan-templates")]
    [InlineData("/plc/mappings")]
    [InlineData("/users")]
    [InlineData("/master/products")]
    public async Task ReadEndpointRequiresSignInButNoRole(string route)
    {
        var policy = await PolicyAsync(route, "GET");
        var authorization = app.Services.GetRequiredService<IAuthorizationService>();
        var reader = new ClaimsPrincipal(new ClaimsIdentity([], "Test"));

        Assert.True((await authorization.AuthorizeAsync(reader, null, policy)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(new ClaimsPrincipal(new ClaimsIdentity()), null, policy)).Succeeded);
    }

    [Theory]
    [InlineData("POST", "/api/v1/production-plans")]
    [InlineData("PUT", "/api/v1/production-plans/{id:guid}")]
    [InlineData("DELETE", "/api/v1/production-plans/{id:guid}")]
    [InlineData("POST", "/api/v1/production-plan-samples")]
    [InlineData("POST", "/api/v1/users")]
    [InlineData("PUT", "/api/v1/users/{id:guid}")]
    [InlineData("DELETE", "/api/v1/users/{id:guid}")]
    [InlineData("PUT", "/api/v1/users/{id:guid}/roles")]
    [InlineData("POST", "/api/v1/users/{id:guid}/reset-password")]
    [InlineData("POST", "/api/v1/plc/fault-mappings/import/commit")]
    public async Task MutationEndpointStillRequiresItsManagementCapability(string method, string route)
    {
        var policy = await PolicyAsync(route, method);
        var authorization = app.Services.GetRequiredService<IAuthorizationService>();
        var reader = new ClaimsPrincipal(new ClaimsIdentity([], "Test"));
        var administrator = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, RoleCapabilityMatrix.Admin)], "Test"));

        Assert.False((await authorization.AuthorizeAsync(reader, null, policy)).Succeeded);
        Assert.True((await authorization.AuthorizeAsync(administrator, null, policy)).Succeeded);
    }

    private async Task<AuthorizationPolicy> PolicyAsync(string route, string method)
    {
        var endpoint = app.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Single(item => item.RoutePattern.RawText?.TrimEnd('/') == route.TrimEnd('/')
                && item.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(method, StringComparer.Ordinal) == true);
        var policy = await AuthorizationPolicy.CombineAsync(app.Services.GetRequiredService<IAuthorizationPolicyProvider>(),
            endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>());
        Assert.NotNull(policy);
        return policy;
    }

}
