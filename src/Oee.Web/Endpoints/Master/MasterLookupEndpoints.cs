using Oee.Application.Contracts.Api;
using Oee.Application.Modules;

namespace Oee.Web.Endpoints;
internal static class MasterLookupEndpoints
{
    // Allow-list ngăn client dùng route lookup để truy vấn tùy ý một tập dữ liệu không được công bố.
    private static readonly HashSet<string> LookupKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "products",
        "shifts",
        "reasons",
        "workunits",
        "workcenters",
        "areas",
        "enterprises",
        "sites",
        "groups",
        "processes"
    };
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/master/lookups/{kind}", async (string kind, IMasterOrganizationQueryService service, CancellationToken ct) =>
        {
            if (!LookupKinds.Contains(kind))
                return Results.NotFound();
            var rows = await service.GetAsync(kind, ct);
            return Results.Ok(rows.Select(x => new LookupResponse(x.Id, x.Code, x.Name, x.Detail, x.Status)));
        }).RequireAuthorization();
    }
}
