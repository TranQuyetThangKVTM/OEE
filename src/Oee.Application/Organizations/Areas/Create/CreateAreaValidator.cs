using Oee.Domain.Common;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Organizations.Areas.Create;

/// <summary>Kiểm tra và chuẩn hóa đầu vào, không đọc hoặc ghi database.</summary>
internal static class CreateAreaValidator
{
    internal static (
        Guid ParentId,
        string Code,
        string Name,
        RecordStatusEnum Status,
        string? Description)
        ValidateArea(CreateAreaCommand command)
        => ValidateScoped(command.SiteId, command.Code, command.Name, command.Status, "Site", command.Description);
}
