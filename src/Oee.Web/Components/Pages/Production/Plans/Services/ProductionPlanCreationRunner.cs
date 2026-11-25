using Microsoft.AspNetCore.Components.Authorization;
using Oee.Application.Common;
using Oee.Application.Contracts.Modules;
using Oee.Application.Modules;
using Oee.Application.Security;
using Oee.Web.Security;

namespace Oee.Web.Components.Pages.Production;

/// <summary>Mỗi lần đọc/preview/tạo dùng scope riêng, giữ principal của circuit cho authorization và audit.</summary>
public sealed class ProductionPlanCreationRunner(IServiceScopeFactory scopes, AuthenticationStateProvider authentication)
{
    public Task<ProductionLookups> LookupsAsync(CancellationToken ct) => InScopeAsync(
        (services, token) => services.GetRequiredService<IProductionModuleQueryService>().LookupsAsync(token), ct);

    public Task<IReadOnlyCollection<SamplePlanListItem>> SearchSamplesAsync(string? search, CancellationToken ct) =>
        InScopeAsync(async (services, token) =>
        {
            var result = await services.GetRequiredService<IProductionModuleQueryService>()
                .SamplesAsync(new SamplePlanListQuery(search, "name", false, 1, 20), token);
            return result.Items;
        }, ct);

    public Task<T> ExecuteAsync<T>(IRequest<T> request, CancellationToken ct) => InScopeAsync(
        (services, token) => services.GetRequiredService<IUseCaseExecutor>().ExecuteAsync(request, token), ct);

    private async Task<T> InScopeAsync<T>(Func<IServiceProvider, CancellationToken, Task<T>> action, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = await authentication.GetAuthenticationStateAsync();
        if (state.User.Identity?.IsAuthenticated != true || !RoleCapabilityMatrix.AnyRoleHasCapability(
            state.User.Claims.Where(claim => claim.Type == System.Security.Claims.ClaimTypes.Role).Select(claim => claim.Value),
            Capabilities.ProductionPlanManage))
            throw new UnauthorizedAccessException("Bạn không có quyền tạo kế hoạch sản xuất.");
        await using var scope = scopes.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ScopedPrincipalAccessor>().User = state.User;
        return await action(scope.ServiceProvider, ct);
    }
}
