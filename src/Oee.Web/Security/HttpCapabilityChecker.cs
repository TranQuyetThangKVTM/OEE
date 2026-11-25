using System.Security.Claims;
using Oee.Application.Security;

namespace Oee.Web.Security;

/// <summary>Adapter đọc role claims của request hiện tại để Application service kiểm tra capability phía server.</summary>
public sealed class HttpCapabilityChecker(IHttpContextAccessor accessor, ScopedPrincipalAccessor? session = null) : ICapabilityChecker
{
    /// <summary>User đã đăng nhập được đọc; quyền ghi/quản lý vẫn lấy từ role.</summary>
    public bool HasCapability(string capability)
    {
        var user = session?.User ?? accessor.HttpContext?.User;
        if (user?.Identities.Any(identity => identity.IsAuthenticated) != true)
            return false;
        return Capabilities.IsReadOnly(capability) || RoleCapabilityMatrix.AnyRoleHasCapability(
            user.Claims.Where(claim => claim.Type == ClaimTypes.Role).Select(claim => claim.Value),
            capability);
    }
}
