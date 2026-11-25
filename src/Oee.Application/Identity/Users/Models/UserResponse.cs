namespace Oee.Application.Contracts.Identity;

/// <summary>DTO user an toàn cho UI/API, không chứa PasswordHash, SecurityStamp, token hoặc secret.</summary>
/// <remarks>ConcurrencyToken ánh xạ ConcurrencyStamp để mutation không ghi đè request mới hơn.</remarks>
public sealed record UserResponse(
    Guid Id,
    string UserName,
    string? DisplayName,
    string? Email,
    bool IsLocked,
    bool IsDisabled,
    IReadOnlyCollection<string> Roles,
    string ConcurrencyToken);
