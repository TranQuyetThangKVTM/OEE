using Oee.Application.Common;
using Oee.Application.Contracts.Identity;
using Oee.Application.Identity.Users.ChangeOwnPassword;
using Oee.Application.Identity.Users.Create;
using Oee.Application.Identity.Users.Disable;
using Oee.Application.Identity.Users.Queries;
using Oee.Application.Identity.Users.ResetPassword;
using Oee.Application.Identity.Users.SetRoles;
using Oee.Application.Identity.Users.Update;
using Oee.Application.Security;
using Oee.Web.Security;
using Oee.Web.Validation;

namespace Oee.Web.Endpoints;

/// <summary>REST API Identity cho đổi mật khẩu cá nhân và quản trị user theo capability.</summary>
public static class UserEndpoints
{
    /// <summary>Map endpoint user; mọi request/response đều dùng DTO không chứa PasswordHash/SecurityStamp.</summary>
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Người dùng đã đăng nhập được đổi mật khẩu của chính mình; service xác định actor từ server context.
        endpoints.MapPut("/users/me/password", async (
                ChangeOwnPasswordRequest request,
                IUseCaseExecutor service,
                CancellationToken ct) =>
            {
                await service.ExecuteAsync(new ChangeOwnPasswordCommand(request), ct);
                return Results.NoContent();
            })
            .RequireAuthorization()
            .AddEndpointFilter<ValidationFilter<ChangeOwnPasswordRequest>>()
            .AddEndpointFilter<AntiforgeryFilter>();

        // Đọc yêu cầu đăng nhập; từng mutation bên dưới vẫn yêu cầu UsersManage tại server.
        var users = endpoints.MapGroup("/users")
            .RequireAuthorization();

        users.MapGet("/", async (
                string? search,
                string? role,
                string? state,
                string? sortBy,
                bool? descending,
                int? page,
                int? pageSize,
                IUseCaseExecutor service,
                CancellationToken ct) =>
            Results.Ok(await service.ExecuteAsync(new SearchUsersQuery(new UserSearchRequest(search, role, state, sortBy, descending ?? false, page ?? 1, pageSize ?? 25)), ct)));

        users.MapGet("/roles", async (IUseCaseExecutor service, CancellationToken ct) =>
            Results.Ok(await service.ExecuteAsync(new ListUserRolesQuery(), ct)));

        users.MapGet("/{id:guid}", async (Guid id, IUseCaseExecutor service, CancellationToken ct) =>
            Results.Ok(await service.ExecuteAsync(new GetUserQuery(id), ct)));

        // UserManager thực hiện password policy/hash; endpoint không tự hash và không log mật khẩu request.
        users.MapPost("/", async (CreateUserRequest request, IUseCaseExecutor service, CancellationToken ct) =>
            {
                var created = await service.ExecuteAsync(new CreateUserCommand(request), ct);
                return Results.Created($"/api/v1/users/{created.Id}", created);
            })
            .AddEndpointFilter<ValidationFilter<CreateUserRequest>>()
            .RequireAuthorization(Capabilities.UsersManage).AddEndpointFilter<AntiforgeryFilter>();

        users.MapPut("/{id:guid}", async (
                Guid id,
                UpdateUserRequest request,
                IUseCaseExecutor service,
                CancellationToken ct) =>
                Results.Ok(await service.ExecuteAsync(new UpdateUserCommand(id, request), ct)))
            .AddEndpointFilter<ValidationFilter<UpdateUserRequest>>()
            .RequireAuthorization(Capabilities.UsersManage).AddEndpointFilter<AntiforgeryFilter>();

        users.MapPut("/{id:guid}/roles", async (Guid id, SetUserRolesRequest request, IUseCaseExecutor service, CancellationToken ct) =>
                Results.Ok(await service.ExecuteAsync(new SetUserRolesCommand(id, request), ct)))
            .AddEndpointFilter<ValidationFilter<SetUserRolesRequest>>()
            .RequireAuthorization(Capabilities.UsersManage).AddEndpointFilter<AntiforgeryFilter>();

        users.MapPost("/{id:guid}/reset-password", async (Guid id, ResetUserPasswordRequest request, IUseCaseExecutor service, CancellationToken ct) =>
            {
                await service.ExecuteAsync(new ResetUserPasswordCommand(id, request), ct);
                return Results.NoContent();
            })
            .AddEndpointFilter<ValidationFilter<ResetUserPasswordRequest>>()
            .RequireAuthorization(Capabilities.UsersManage).AddEndpointFilter<AntiforgeryFilter>();

        // DELETE là vô hiệu hóa/soft-delete theo service và dùng If-Match để chống ghi đè trạng thái mới.
        users.MapDelete("/{id:guid}", async (Guid id, HttpRequest request, IUseCaseExecutor service, CancellationToken ct) =>
            {
                var concurrencyToken = request.Headers.IfMatch.ToString().Trim('"');
                await service.ExecuteAsync(new DisableUserCommand(id, concurrencyToken), ct);
                return Results.NoContent();
            })
            .RequireAuthorization(Capabilities.UsersManage).AddEndpointFilter<AntiforgeryFilter>();

        return endpoints;
    }
}
