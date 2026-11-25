using Oee.Domain.Common;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Organizations.Enterprises.Create;

/// <summary>
/// Kiểm tra và chuẩn hóa đầu vào, không đọc hoặc ghi database.
/// </summary>
internal static class CreateEnterpriseValidator
{
    internal static (
        string Code,
        string Name,
        string? Description,
        RecordStatusEnum Status)
        ValidateEnterprise(CreateEnterpriseCommand command)
    {
        return (
            Code: Required(command.Code, 32, "Enterprise code"),
            Name: Required(command.Name, 200, "Enterprise name"),
            Description: Optional(command.Description, 500, "Description"),
            Status: Status(command.Status));
    }
}
