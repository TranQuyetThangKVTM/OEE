using Oee.Application.Audit;
using Oee.Application.Audit.Retention.PreviewRetention;
using Oee.Application.Audit.Retention.RunRetention;
using Oee.Application.Common;
using Oee.Application.Contracts.Audit;
using Oee.Application.Security;
using Oee.Web.Security;
using Oee.Web.Validation;

namespace Oee.Web.Endpoints;

/// <summary>Định nghĩa REST API đọc AuditLog và thực hiện retention có kiểm soát dưới prefix /api/v1.</summary>
public static class AuditEndpoints
{
    /// <summary>Đăng ký endpoint query/detail/retention cùng policy và validation tương ứng.</summary>
    /// <param name="endpoints">Route builder của nhóm API v1.</param>
    /// <returns>Route builder để Program có thể tiếp tục map endpoint khác.</returns>
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // GET /audit: filter/sort/page đều chuyển thành request DTO; service thực hiện query SQL phía server.
        endpoints.MapGet("/audit", async (
                string? user,
                string? entityType,
                string? action,
                DateTimeOffset? from,
                DateTimeOffset? to,
                string? sortBy,
                bool? descending,
                int? page,
                int? pageSize,
                IAuditQueryService service,
                CancellationToken ct) =>
                Results.Ok(await service.SearchAsync(
                    new AuditLogSearchRequest(
                        user,
                        entityType,
                        action,
                        from,
                        to,
                        sortBy,
                        descending ?? true,
                        page ?? 1,
                        pageSize ?? 25),
                    ct)))
            .RequireAuthorization();

        // GET /audit/{id}: trả presentation detail; không cung cấp endpoint update/delete từng audit entry.
        endpoints.MapGet("/audit/{id:guid}", async (
                Guid id,
                IAuditQueryService service,
                CancellationToken ct) => Results.Ok(await service.DetailAsync(id, ct)))
            .RequireAuthorization();

        // Preview và run retention dùng capability mạnh hơn AuditRead. Mutation còn phải qua antiforgery.
        endpoints.MapGet("/audit/retention/preview", async (
                IUseCaseExecutor service,
                CancellationToken ct) => Results.Ok(await service.ExecuteAsync(new PreviewRetentionAuditQuery(), ct)))
            .RequireAuthorization(Capabilities.AuditRetentionManage);

        endpoints.MapPost("/audit/retention", async (
                AuditRetentionRequest request,
                IUseCaseExecutor service,
                CancellationToken ct) =>
                Results.Ok(await service.ExecuteAsync(new RunRetentionAuditCommand(request), ct)))
            .RequireAuthorization(Capabilities.AuditRetentionManage)
            .AddEndpointFilter<ValidationFilter<AuditRetentionRequest>>()
            .AddEndpointFilter<AntiforgeryFilter>();

        return endpoints;
    }
}
