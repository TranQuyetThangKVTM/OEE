namespace Oee.Application.Identity;

/// <summary>Thông tin tài khoản cho use case, không chứa hash mật khẩu hoặc kiểu ASP.NET Identity.</summary>
public sealed class ManagedUserAccount
{
    public Guid Id
    {
        get; set;
    }
    public string? UserName
    {
        get; set;
    }
    public string? DisplayName
    {
        get; set;
    }
    public string? Email
    {
        get; set;
    }
    public bool EmailConfirmed
    {
        get; set;
    }
    public bool LockoutEnabled
    {
        get; set;
    }
    public DateTimeOffset? LockoutEnd
    {
        get; set;
    }
    public bool IsDisabled
    {
        get; set;
    }
    public string? ConcurrencyStamp
    {
        get; set;
    }
}
