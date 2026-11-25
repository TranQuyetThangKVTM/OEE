using System.ComponentModel.DataAnnotations;

namespace Oee.Application.Contracts.Identity;

/// <summary>Request sửa profile/role; username được server kiểm tra bất biến.</summary>
public sealed record UpdateUserRequest(
    [Required, StringLength(256, MinimumLength = 3)] string UserName,
    [EmailAddress, StringLength(256)] string? Email,
    [Required, StringLength(120)] string DisplayName,
    [Required, MinLength(1)] IReadOnlyCollection<string> Roles,
    [Required] string ConcurrencyToken);
