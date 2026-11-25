using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Oee.Targets.AddStandardVersion;
using Oee.Application.Oee.Targets.AddTargetVersion;
using Oee.Application.Security;
using Oee.Web.Security;

namespace Oee.Web.Endpoints;
internal static class EffectiveConfigurationEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/standards/versions", async (EffectiveStandardRequest request, IUseCaseExecutor service, CancellationToken ct) => Results.Ok(new { Id = await service.ExecuteAsync(new AddStandardVersionCommand(request), ct) })).RequireAuthorization(Capabilities.MasterDataWrite).AddEndpointFilter<AntiforgeryFilter>();
        endpoints.MapPost("/oee-targets/versions", async (EffectiveTargetRequest request, IUseCaseExecutor service, CancellationToken ct) => Results.Ok(new { Id = await service.ExecuteAsync(new AddTargetVersionCommand(request), ct) })).RequireAuthorization(Capabilities.TargetsManage).AddEndpointFilter<AntiforgeryFilter>();
    }
}
