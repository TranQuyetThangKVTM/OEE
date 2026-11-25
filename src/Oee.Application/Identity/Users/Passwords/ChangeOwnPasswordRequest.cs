using System.ComponentModel.DataAnnotations;

namespace Oee.Application.Contracts.Identity;

/// <summary>Request người đang đăng nhập tự đổi password sau khi xác minh password hiện tại.</summary>
public sealed record ChangeOwnPasswordRequest(
    [Required, StringLength(128)] string CurrentPassword,
    [Required, StringLength(128, MinimumLength = 8)] string NewPassword);
