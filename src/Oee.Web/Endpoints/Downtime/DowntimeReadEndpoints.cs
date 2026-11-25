using Oee.Application.Contracts.Api;
using Oee.Application.Modules;
using Oee.Web.Validation;

namespace Oee.Web.Endpoints;
internal static class DowntimeReadEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/downtime", async ([AsParameters] PageRequest request, IProductionModuleQueryService service, CancellationToken ct) =>
        {
            var result = await service.DowntimeAsync(new Oee.Application.Contracts.Modules.DowntimeListQuery(request.Search, SortBy: request.Sort, Descending: request.Descending, Page: request.Page, PageSize: request.PageSize), ct);
            return Results.Ok(new PageResponse<Oee.Application.Contracts.Modules.DowntimeListItem>(result.Items, request.Page, request.PageSize, result.TotalCount));
        }).RequireAuthorization().AddEndpointFilter<ValidationFilter<PageRequest>>();
    }
}
