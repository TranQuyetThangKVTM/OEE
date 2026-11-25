using Oee.Application.Security;

namespace Oee.Web.Security;

/// <summary>Đăng ký authorization policy theo capability tập trung thay vì rải tên role trong UI/API.</summary>
public static class AuthorizationExtensions
{
    /// <summary>Quyền xem yêu cầu đăng nhập; quyền ghi/quản lý tiếp tục kiểm tra role claims.</summary>
    public static IServiceCollection AddCapabilityAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            foreach (var capability in Capabilities.All)
            {
                options.AddPolicy(capability, policy =>
                {
                    policy.RequireAuthenticatedUser();
                    if (!Capabilities.IsReadOnly(capability))
                    {
                        policy.RequireAssertion(context => RoleCapabilityMatrix.AnyRoleHasCapability(
                            context.User.Claims
                                .Where(claim => claim.Type == System.Security.Claims.ClaimTypes.Role)
                                .Select(claim => claim.Value),
                            capability));
                    }
                });
            }
        });
        return services;
    }
}
