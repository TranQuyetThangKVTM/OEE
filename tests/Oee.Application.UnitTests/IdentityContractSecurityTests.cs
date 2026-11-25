using Oee.Application.Contracts.Identity;

namespace Oee.Application.UnitTests;

/// <summary>Security regression test bảo đảm DTO người dùng không làm rò bí mật ASP.NET Identity.</summary>
public sealed class IdentityContractSecurityTests
{
    [Fact]
    public void UserResponseDoesNotExposeIdentitySecrets()
    {
        // Arrange + Act: dùng reflection lấy public properties thực sự được UI/API nhìn thấy.
        var propertyNames = typeof(UserResponse)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        // Assert: các trường xác thực nhạy cảm không được trở thành một phần public contract.
        Assert.DoesNotContain("PasswordHash", propertyNames, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("SecurityStamp", propertyNames, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("AuthenticatorKey", propertyNames, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("RecoveryCodes", propertyNames, StringComparer.OrdinalIgnoreCase);
    }
}
