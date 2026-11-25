using System.Security.Claims;

namespace Oee.Web.Security;

/// <summary>Principal từ AuthenticationStateProvider, đặt trong scope riêng của chat hoặc thao tác dialog.</summary>
public sealed class ScopedPrincipalAccessor
{
    public ClaimsPrincipal? User { get; set; }
}
