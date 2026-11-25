using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Production.Plans.Correct;
using Oee.Application.Production.Plans.Create;
using Oee.Application.Production.Plans.Delete;
using Oee.Application.Production.Plans.GenerateFromSample;
using Oee.Application.Production.Plans.PreviewBulk;
using Oee.Application.Production.Plans.Queries;
using Oee.Application.Production.Plans.Update;
using Oee.Application.Security;
using Oee.Web.Security;
using static Oee.Web.Endpoints.EndpointConcurrency;

namespace Oee.Web.Endpoints;
internal static class ProductionPlanEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        // Đọc yêu cầu đăng nhập; mutation yêu cầu ProductionPlanManage và kiểm tra CSRF.
        var plans = endpoints.MapGroup("/production-plans").RequireAuthorization();
        plans.MapGet("/", async (DateTimeOffset? from, DateTimeOffset? until, int page, int pageSize, IUseCaseExecutor service, CancellationToken ct) => Results.Ok(await service.ExecuteAsync(new ListProductionPlansQuery(from, until, page, pageSize), ct)));
        plans.MapGet("/{id:guid}", async (Guid id, HttpContext context, IUseCaseExecutor service, CancellationToken ct) =>
        {
            var response = await service.ExecuteAsync(new GetProductionPlanQuery(id), ct);
            context.Response.Headers.ETag = $"\"{response.RowVersion}\"";
            return Results.Ok(response);
        });
        plans.MapPost("/", async (ProductionPlanRequest request, IUseCaseExecutor service, CancellationToken ct) => Results.Ok(await service.ExecuteAsync(new CreateProductionPlanCommand(request), ct))).RequireAuthorization(Capabilities.ProductionPlanManage).AddEndpointFilter<AntiforgeryFilter>();
        // PUT yêu cầu If-Match chứa RowVersion Base64. Thiếu token trả 428, token cũ được EF chuyển thành 412.
        plans.MapPut("/{id:guid}", async (Guid id, ProductionPlanRequest request, HttpContext context, IUseCaseExecutor service, CancellationToken ct) =>
        {
            var value = context.Request.Headers.IfMatch.ToString().Trim('"');
            if (string.IsNullOrWhiteSpace(value))
                return Results.Problem(statusCode: 428, title: "If-Match is required.");
            byte[] version;
            try
            {
                version = Convert.FromBase64String(value);
            }
            catch (FormatException)
            {
                return Results.Problem(statusCode: 400, title: "Invalid ETag.");
            }

            var response = await service.ExecuteAsync(new UpdateProductionPlanCommand(id, request, version), ct);
            context.Response.Headers.ETag = $"\"{response.RowVersion}\"";
            return Results.Ok(response);
        }).RequireAuthorization(Capabilities.ProductionPlanManage).AddEndpointFilter<AntiforgeryFilter>();
        plans.MapPost("/{id:guid}/corrections", async (Guid id, ProductionPlanRequest request, HttpContext context, IUseCaseExecutor service, CancellationToken ct) =>
        {
            var version = ReadIfMatch(context);
            return version is null ? Results.Problem(statusCode: 428, title: "If-Match is required.") : Results.Ok(await service.ExecuteAsync(new CorrectProductionPlanCommand(id, request, version), ct));
        }).RequireAuthorization(Capabilities.ProductionPlanManage).AddEndpointFilter<AntiforgeryFilter>();
        plans.MapDelete("/{id:guid}", async (Guid id, HttpContext context, IUseCaseExecutor service, CancellationToken ct) =>
        {
            var version = ReadIfMatch(context);
            if (version is null)
                return Results.Problem(statusCode: 428, title: "If-Match is required.");
            await service.ExecuteAsync(new DeleteProductionPlanCommand(id, version), ct);
            return Results.NoContent();
        }).RequireAuthorization(Capabilities.ProductionPlanManage).AddEndpointFilter<AntiforgeryFilter>();
        plans.MapPost("/generate-from-sample", async (GenerateSamplePlanRequest request, IUseCaseExecutor service, CancellationToken ct) => Results.Ok(new { Created = await service.ExecuteAsync(new GenerateProductionPlansFromSampleCommand(request), ct) })).RequireAuthorization(Capabilities.ProductionPlanManage).AddEndpointFilter<AntiforgeryFilter>();
        plans.MapPost("/bulk-preview", async (BulkProductionPlanRequest request, IUseCaseExecutor service, CancellationToken ct) => Results.Ok(await service.ExecuteAsync(new PreviewProductionPlansQuery(request), ct))).RequireAuthorization(Capabilities.ProductionPlanManage).AddEndpointFilter<AntiforgeryFilter>();
        plans.MapPost("/bulk", async (BulkProductionPlanRequest request, IUseCaseExecutor service, CancellationToken ct) => Results.Ok(await service.ExecuteAsync(new CreateProductionPlansCommand(request), ct))).RequireAuthorization(Capabilities.ProductionPlanManage).AddEndpointFilter<AntiforgeryFilter>();
    }
}
