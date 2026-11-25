using Microsoft.AspNetCore.Components.Authorization;
using Oee.Application.Chat;
using Oee.Application.Common;
using Oee.Application.Contracts.Modules;
using Oee.Application.Modules;
using Oee.Application.Production.Automation;
using Oee.Application.Security;
using Oee.Web.Security;

namespace Oee.Web.Chat;

/// <summary>Scope riêng để chat không dùng chung DbContext đang chạy với trang hiện tại.</summary>
public sealed class ChatRequestRunner(IServiceScopeFactory scopes, AuthenticationStateProvider authentication)
{
    public async Task<ChatAnswerDto> AskAsync(string question, CancellationToken ct)
    {
        var state = await authentication.GetAuthenticationStateAsync();
        if (state.User.Identity?.IsAuthenticated != true) throw new UnauthorizedAccessException("Bạn cần đăng nhập để dùng trợ lý OEE.");
        await using var scope = scopes.CreateAsyncScope();
        SetPrincipal(scope.ServiceProvider, state.User);
        return await scope.ServiceProvider.GetRequiredService<IUseCaseExecutor>().ExecuteAsync(new AskChatbotQuery(question), ct);
    }

    public async Task<ProductionPlanSchedule?> ScheduleAsync(CancellationToken ct) =>
        await ExecuteAsync(new GetProductionPlanScheduleQuery(), ct);

    public async Task<ProductionPlanSchedule> SaveScheduleAsync(SaveProductionPlanScheduleCommand command, CancellationToken ct) =>
        await ExecuteAsync(command, ct);

    public async Task<IReadOnlyCollection<SamplePlanListItem>> SearchSamplesAsync(string? search, CancellationToken ct)
    {
        var state = await authentication.GetAuthenticationStateAsync();
        EnsureManage(state);
        await using var scope = scopes.CreateAsyncScope();
        SetPrincipal(scope.ServiceProvider, state.User);
        var result = await scope.ServiceProvider.GetRequiredService<IProductionModuleQueryService>()
            .SamplesAsync(new SamplePlanListQuery(search, "code", false, 1, 20), ct);
        return result.Items;
    }

    private async Task<T> ExecuteAsync<T>(IRequest<T> request, CancellationToken ct)
    {
        var state = await authentication.GetAuthenticationStateAsync();
        EnsureManage(state);
        await using var scope = scopes.CreateAsyncScope();
        SetPrincipal(scope.ServiceProvider, state.User);
        return await scope.ServiceProvider.GetRequiredService<IUseCaseExecutor>().ExecuteAsync(request, ct);
    }

    private static void SetPrincipal(IServiceProvider services, System.Security.Claims.ClaimsPrincipal user)
    {
        var principal = services.GetService<ScopedPrincipalAccessor>();
        if (principal is not null) principal.User = user;
    }

    private static void EnsureManage(AuthenticationState state)
    {
        if (state.User.Identity?.IsAuthenticated != true
            || !RoleCapabilityMatrix.AnyRoleHasCapability(state.User.Claims
                .Where(claim => claim.Type == System.Security.Claims.ClaimTypes.Role).Select(claim => claim.Value), Capabilities.ProductionPlanManage))
            throw new UnauthorizedAccessException("Bạn cần quyền quản lý kế hoạch sản xuất để thay đổi lịch tự động.");
    }
}
