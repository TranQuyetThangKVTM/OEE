using System.ComponentModel.DataAnnotations;
using System.Data;
using Oee.Application.Common;
using Oee.Application.Contracts.Identity;

namespace Oee.Application.Identity.Users.Common;

/// <summary>Kiểm tra và chuẩn hóa đầu vào, không đọc hoặc ghi database.</summary>
internal static class UserManagementValidator
{
    internal static void ValidateRequest(object request)
    {
        var validationResults = new List<ValidationResult>();

        if (!Validator.TryValidateObject(request, new ValidationContext(request), validationResults, true))
        {
            throw new ApplicationRuleException(string.Join("; ", validationResults.Select(result => result.ErrorMessage)));
        }

        var email = request switch
        {
            CreateUserRequest create => create.Email,
            UpdateUserRequest update => update.Email,
            _ => null,
        };

        if (!string.IsNullOrWhiteSpace(email) && !new EmailAddressAttribute().IsValid(email.Trim()))
        {
            throw new ApplicationRuleException("Email is not valid.");
        }
    }
}
