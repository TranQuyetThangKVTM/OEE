using Oee.Application.Common;
using Oee.Application.Contracts.Identity;
using Oee.Application.Identity;
using Oee.Application.Security;
namespace Oee.Tests.UseCases;
/// <summary>Test scenario adapter: chạy handler thật trên bộ ports được truyền vào, không chứa nghiệp vụ.</summary>
internal sealed class UserManagementScenario : IUserManagementScenario
{
    private readonly IUseCaseExecutor requests;
    public UserManagementScenario(IUseCaseExecutor requests) => this.requests = requests;
    public UserManagementScenario(IIdentityAccountGateway accounts, IApplicationUnitOfWork unitOfWork, ICurrentActor currentActor, IClock clock, ICapabilityChecker? capabilityChecker = null)
    {
        requests = new ScenarioHandlerExecutor(new Dictionary<Type, object?> { [typeof(IIdentityAccountGateway)] = accounts, [typeof(IApplicationUnitOfWork)] = unitOfWork, [typeof(ICurrentActor)] = currentActor, [typeof(IClock)] = clock, [typeof(ICapabilityChecker)] = capabilityChecker });
    }

    public Task<PagedUserResponse> SearchAsync(UserSearchRequest request, CancellationToken cancellationToken) => this.requests.ExecuteAsync(new global::Oee.Application.Identity.Users.Queries.SearchUsersQuery(request), cancellationToken);
    public Task<UserResponse> GetAsync(Guid userId, CancellationToken cancellationToken) => this.requests.ExecuteAsync(new global::Oee.Application.Identity.Users.Queries.GetUserQuery(userId), cancellationToken);
    public Task<IReadOnlyCollection<UserRoleResponse>> RolesAsync(CancellationToken cancellationToken) => this.requests.ExecuteAsync(new global::Oee.Application.Identity.Users.Queries.ListUserRolesQuery(), cancellationToken);
    public Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken) => this.requests.ExecuteAsync(new global::Oee.Application.Identity.Users.Create.CreateUserCommand(request), cancellationToken);
    public Task<UserResponse> UpdateAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken) => this.requests.ExecuteAsync(new global::Oee.Application.Identity.Users.Update.UpdateUserCommand(userId, request), cancellationToken);
    public Task<UserResponse> SetRolesAsync(Guid userId, SetUserRolesRequest request, CancellationToken cancellationToken) => this.requests.ExecuteAsync(new global::Oee.Application.Identity.Users.SetRoles.SetUserRolesCommand(userId, request), cancellationToken);
    public Task ResetPasswordAsync(Guid userId, ResetUserPasswordRequest request, CancellationToken cancellationToken) => this.requests.ExecuteAsync(new global::Oee.Application.Identity.Users.ResetPassword.ResetUserPasswordCommand(userId, request), cancellationToken);
    public Task DisableAsync(Guid userId, string concurrencyToken, CancellationToken cancellationToken) => this.requests.ExecuteAsync(new global::Oee.Application.Identity.Users.Disable.DisableUserCommand(userId, concurrencyToken), cancellationToken);
    public Task ChangeOwnPasswordAsync(ChangeOwnPasswordRequest request, CancellationToken cancellationToken) => this.requests.ExecuteAsync(new global::Oee.Application.Identity.Users.ChangeOwnPassword.ChangeOwnPasswordCommand(request), cancellationToken);
}
