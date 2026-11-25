using Oee.Application.Contracts.Api;
using Oee.Application.Modules;
using Oee.Web.Validation;

namespace Oee.Web.Endpoints;
internal static class ProductionReadEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        // Live status được projection lần nữa sang contract API; typed SignalR/API không lộ navigation property.
        endpoints.MapGet("/production/status", async (ILiveOeeQueryService service, CancellationToken ct) =>
        {
            var model = await service.GetAsync(new Oee.Application.Contracts.Modules.LiveOeeQuery(), ct);
            return Results.Ok(new ProductionStatusResponse(model.UpdatedAt, model.Machines.Select(x => new DeviceStateResponse(x.WorkUnitId, x.Code, x.Name, x.OperationalState, x.ConnectivityState, x.Oee)).ToArray()));
        }).RequireAuthorization();
        // Các list endpoint thống nhất PageRequest/PageResponse và thực hiện paging trong Application/SQL.
        endpoints.MapGet("/production/plans", async ([AsParameters] PageRequest request, IProductionModuleQueryService service, CancellationToken ct) =>
        {
            var result = await service.PlansAsync(new Oee.Application.Contracts.Modules.ProductionPlanListQuery(request.Search, SortBy: request.Sort, Descending: request.Descending, Page: request.Page, PageSize: request.PageSize), ct);
            return Results.Ok(new PageResponse<Oee.Application.Contracts.Modules.ProductionPlanListItem>(result.Items, request.Page, request.PageSize, result.TotalCount));
        }).RequireAuthorization().AddEndpointFilter<ValidationFilter<PageRequest>>();
    }
}
