using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Downtime.Events.BulkReclassify;
using Oee.Application.Downtime.Events.Close;
using Oee.Application.Downtime.Events.Create;
using Oee.Application.Downtime.Events.Reclassify;
using Oee.Application.Security;
using Oee.Web.Security;

namespace Oee.Web.Endpoints;
internal static class DowntimeCommandEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        // Downtime có thể mở với EndAt null; close/reclassify dùng concurrency token từ DTO.
        var downtime = endpoints.MapGroup("/downtime").RequireAuthorization(Capabilities.DowntimeWrite);
        downtime.MapPost("/", async (DowntimeRequest request, IUseCaseExecutor service, CancellationToken ct) => Results.Ok(new { Id = await service.ExecuteAsync(new CreateDowntimeEventCommand(request), ct) })).AddEndpointFilter<AntiforgeryFilter>();
        downtime.MapPost("/{id:guid}/close", async (Guid id, CloseDowntimeRequest request, IUseCaseExecutor service, CancellationToken ct) =>
        {
            await service.ExecuteAsync(new CloseDowntimeEventCommand(id, request.EndAt, request.ConcurrencyToken), ct);
            return Results.NoContent();
        }).AddEndpointFilter<AntiforgeryFilter>();
        downtime.MapPost("/{id:guid}/reclassify", async (Guid id, ReclassifyDowntimeRequest request, IUseCaseExecutor service, CancellationToken ct) =>
        {
            await service.ExecuteAsync(new ReclassifyDowntimeEventCommand(id, request.DowntimeReasonId, request.ConcurrencyToken), ct);
            return Results.NoContent();
        }).AddEndpointFilter<AntiforgeryFilter>();
        downtime.MapPost("/bulk-reclassify", async (BulkReclassifyDowntimeRequest request, IUseCaseExecutor service, CancellationToken ct) => Results.Ok(new { Updated = await service.ExecuteAsync(new BulkReclassifyDowntimeEventCommand(request.Items, request.DowntimeReasonId), ct) })).AddEndpointFilter<AntiforgeryFilter>();
    }
}
