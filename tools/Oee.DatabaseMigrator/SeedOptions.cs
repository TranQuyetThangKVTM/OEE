namespace Oee.DatabaseMigrator;

/// <summary>
/// Cấu hình tài khoản quản trị chỉ dành cho DatabaseMigrator/development seed.
/// </summary>
/// <remarks>Password không có giá trị mặc định và phải được cấp qua User Secrets hoặc environment variable.</remarks>
public sealed class SeedOptions
{
    /// <summary>Tên section cấu hình được bind bởi Options pattern.</summary>
    public const string SectionName = "Seed";
    /// <summary>Username Admin development; business command không cho đổi username qua dialog edit.</summary>
    public string AdminUserName { get; init; } = "admin";
    /// <summary>Email ban đầu của tài khoản development.</summary>
    public string AdminEmail { get; init; } = "admin@oee.local";
    /// <summary>Tên hiển thị thân thiện trong AppBar và Audit.</summary>
    public string AdminDisplayName { get; init; } = "OEE Administrator";
    /// <summary>Mật khẩu dạng rõ chỉ sống trong cấu hình lúc chạy và không được log/ghi audit.</summary>
    public string? AdminPassword { get; init; }
}
