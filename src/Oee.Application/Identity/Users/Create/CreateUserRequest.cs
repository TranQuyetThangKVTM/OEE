using System.ComponentModel.DataAnnotations;

namespace Oee.Application.Contracts.Identity;

/// <summary>Request tạo user; password chỉ đi vào server Identity và không xuất hiện trong response/audit.</summary>
public sealed record CreateUserRequest(
    [Required, StringLength(256, MinimumLength = 3)] string UserName,
    [EmailAddress, StringLength(256)] string? Email,
    [Required, StringLength(120)] string DisplayName,
    [Required, StringLength(128, MinimumLength = 8)] string Password,
    [Required, MinLength(1)] IReadOnlyCollection<string> Roles);
