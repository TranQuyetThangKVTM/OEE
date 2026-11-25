using Oee.Application.Common;
using Oee.Application.Reporting.Importing.FaultMappings.Import;
using Oee.Application.Reporting.Importing.FaultMappings.Preview;
using Oee.Application.Reporting.Importing.FaultMappings.Queries;
using Oee.Application.Security;
using Oee.Web.Security;

namespace Oee.Web.Endpoints;
internal static class FaultMappingImportEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        // Nhóm upload được cấu hình antiforgery rõ ở từng POST; GET template không làm thay đổi trạng thái.
        var import = endpoints.MapGroup("/plc/fault-mappings/import").RequireAuthorization().DisableAntiforgery();
        import.MapGet("/template", async (IUseCaseExecutor service) => Results.File(await service.ExecuteAsync(new CreateTemplateFaultMappingsQuery(), default), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "fault-mappings-v1.xlsx"));
        import.MapPost("/preview", async (IFormFile file, IUseCaseExecutor service, CancellationToken ct) =>
        {
            await using var stream = file.OpenReadStream();
            return Results.Ok(await service.ExecuteAsync(new PreviewFaultMappingsQuery(stream, file.Length), ct));
        }).RequireAuthorization(Capabilities.PlcManage).AddEndpointFilter<AntiforgeryFilter>();
        import.MapPost("/commit", async (IFormFile file, IUseCaseExecutor service, CancellationToken ct) =>
        {
            await using var stream = file.OpenReadStream();
            return Results.Ok(await service.ExecuteAsync(new ImportFaultMappingsCommand(stream, file.Length), ct));
        }).RequireAuthorization(Capabilities.PlcManage).AddEndpointFilter<AntiforgeryFilter>();
    }
}
