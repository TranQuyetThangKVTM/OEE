using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Production.Samples.AddItems;
using Oee.Application.Production.Samples.Create;
using Oee.Application.Production.Samples.PreviewAddItems;
using Oee.Application.Production.Samples.Update;
using Oee.Application.Security;
using Oee.Web.Security;
using static Oee.Web.Endpoints.EndpointConcurrency;

namespace Oee.Web.Endpoints;
internal static class SamplePlanEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        // Các bulk command của mẫu trả typed result và transaction/idempotency được xử lý phía service.
        var samples = endpoints.MapGroup("/production-plan-samples").RequireAuthorization(Capabilities.ProductionPlanManage);
        samples.MapPost("/", async (SamplePlanRequest request, IUseCaseExecutor service, CancellationToken ct) => Results.Ok(await service.ExecuteAsync(new CreateSamplePlanCommand(request), ct))).AddEndpointFilter<AntiforgeryFilter>();
        samples.MapPost("/bulk", async (SamplePlanBulkCreateRequest request, IUseCaseExecutor service, CancellationToken ct) => Results.Ok(await service.ExecuteAsync(new CreateSamplePlanWithItemsCommand(request), ct))).AddEndpointFilter<AntiforgeryFilter>();
        samples.MapPost("/{id:guid}/items/bulk", async (Guid id, SamplePlanBulkAddItemsRequest request, IUseCaseExecutor service, CancellationToken ct) => Results.Ok(await service.ExecuteAsync(new AddSamplePlanItemsCommand(id, request), ct))).AddEndpointFilter<AntiforgeryFilter>();
        samples.MapPost("/{id:guid}/items/bulk-preview", async (Guid id, SamplePlanBulkAddItemsRequest request, IUseCaseExecutor service, CancellationToken ct) => Results.Ok(await service.ExecuteAsync(new PreviewSamplePlanItemsQuery(id, request), ct))).AddEndpointFilter<AntiforgeryFilter>();
        samples.MapPut("/{id:guid}", async (Guid id, SamplePlanRequest request, HttpContext context, IUseCaseExecutor service, CancellationToken ct) =>
        {
            var version = ReadIfMatch(context);
            return version is null ? Results.Problem(statusCode: 428, title: "If-Match is required.") : Results.Ok(await service.ExecuteAsync(new UpdateSamplePlanCommand(id, request, version), ct));
        }).AddEndpointFilter<AntiforgeryFilter>();
    }
}
