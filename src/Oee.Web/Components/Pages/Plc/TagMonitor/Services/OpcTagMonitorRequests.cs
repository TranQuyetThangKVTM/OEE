using Microsoft.AspNetCore.Components.Authorization;
using Oee.Application.Common;
using Oee.Web.Security;

namespace Oee.Web.Components.Pages.Plc;

/// <summary>Scope ngắn cho mỗi thao tác; timer màn hình không dùng chung DbContext của circuit.</summary>
public sealed class OpcTagMonitorRequests(IServiceScopeFactory scopes, AuthenticationStateProvider authentication)
{
    public async Task<T> ExecuteAsync<T>(IRequest<T> request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var state = await authentication.GetAuthenticationStateAsync();
        if (!state.User.Identities.Any(identity => identity.IsAuthenticated))
            throw new UnauthorizedAccessException("Đăng nhập để giám sát OPC UA.");
        await using var scope = scopes.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ScopedPrincipalAccessor>().User = state.User;
        return await scope.ServiceProvider.GetRequiredService<IUseCaseExecutor>().ExecuteAsync(request, ct);
    }
}
