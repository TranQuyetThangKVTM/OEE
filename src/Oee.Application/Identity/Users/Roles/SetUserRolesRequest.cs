using System.ComponentModel.DataAnnotations;

namespace Oee.Application.Contracts.Identity;

/// <summary>Request đổi role theo chênh lệch và optimistic concurrency.</summary>
public sealed record SetUserRolesRequest(
    [Required, MinLength(1)] IReadOnlyCollection<string> Roles,
    [Required] string ConcurrencyToken);
