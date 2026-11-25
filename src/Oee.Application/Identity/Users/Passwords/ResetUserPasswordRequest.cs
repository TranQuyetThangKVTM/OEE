using System.ComponentModel.DataAnnotations;

namespace Oee.Application.Contracts.Identity;

/// <summary>Request Admin reset password bằng API Identity; plaintext không được log/audit.</summary>
public sealed record ResetUserPasswordRequest(
    [Required, StringLength(128, MinimumLength = 8)] string NewPassword,
    [Required] string ConcurrencyToken);
