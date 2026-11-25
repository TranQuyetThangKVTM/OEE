using Oee.Application.Common;
using Oee.Application.Contracts.Api;
using Oee.Application.Oee.Alerts.Acknowledge;
using Oee.Application.Oee.Alerts.Queries;
using Oee.Application.Oee.Alerts.Reevaluate;
using Oee.Application.Oee.Alerts.Resolve;
using Oee.Application.Security;
using Oee.Web.Security;
using Oee.Web.Validation;

namespace Oee.Web.Endpoints;
internal static class AlertApiEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/alerts", async ([AsParameters] PageRequest request, IUseCaseExecutor service, CancellationToken ct) =>
        {
            var result = await service.ExecuteAsync(new ListOeeAlertsQuery(new Oee.Application.Contracts.Modules.AlertListQuery(request.Search, SortBy: request.Sort, Descending: request.Descending, Page: request.Page, PageSize: request.PageSize)), ct);
            var items = result.Items.Select(x => new AlertResponse(x.Id, x.WorkUnit, x.Metric, x.Severity, x.State, x.Actual, x.Target, x.TriggeredAt, x.ConcurrencyToken)).ToArray();
            return Results.Ok(new PageResponse<AlertResponse>(items, request.Page, request.PageSize, result.TotalCount));
        }).RequireAuthorization().AddEndpointFilter<ValidationFilter<PageRequest>>();
        // Mutation alert cần cả server-side capability, DTO validation và antiforgery do web dùng cookie auth.
        endpoints.MapPost("/alerts/{id:guid}/acknowledgement", async (Guid id, AlertAcknowledgeRequest request, IUseCaseExecutor service, CancellationToken ct) =>
        {
            await service.ExecuteAsync(new AcknowledgeOeeAlertCommand(id, new Oee.Application.Contracts.Modules.AlertMutationRequest(request.ConcurrencyToken, request.Note)), ct);
            return Results.NoContent();
        }).RequireAuthorization(Capabilities.AlertsManage).AddEndpointFilter<ValidationFilter<AlertAcknowledgeRequest>>().AddEndpointFilter<AntiforgeryFilter>();
        endpoints.MapPost("/alerts/{id:guid}/resolution", async (Guid id, AlertResolveRequest request, IUseCaseExecutor service, CancellationToken ct) =>
        {
            await service.ExecuteAsync(new ResolveOeeAlertCommand(id, new Oee.Application.Contracts.Modules.AlertMutationRequest(request.ConcurrencyToken, request.Note)), ct);
            return Results.NoContent();
        }).RequireAuthorization(Capabilities.AlertsManage).AddEndpointFilter<ValidationFilter<AlertResolveRequest>>().AddEndpointFilter<AntiforgeryFilter>();
        endpoints.MapPost("/alerts/{id:guid}/reevaluation", async (Guid id, AlertReevaluateRequest request, IUseCaseExecutor service, CancellationToken ct) =>
        {
            await service.ExecuteAsync(new ReevaluateOeeAlertCommand(id, request.ConcurrencyToken), ct);
            return Results.NoContent();
        }).RequireAuthorization(Capabilities.SnapshotsManage).AddEndpointFilter<ValidationFilter<AlertReevaluateRequest>>().AddEndpointFilter<AntiforgeryFilter>();
    }
}
