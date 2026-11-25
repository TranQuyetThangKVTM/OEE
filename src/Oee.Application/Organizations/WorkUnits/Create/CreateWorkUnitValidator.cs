using Oee.Domain.Common;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Organizations.WorkUnits.Create;

/// <summary>Kiểm tra và chuẩn hóa đầu vào, không đọc hoặc ghi database.</summary>
internal static class CreateWorkUnitValidator
{
    internal static (
        Guid ParentId,
        string Code,
        string Name,
        RecordStatusEnum Status,
        string? Description)
        ValidateWorkUnit(CreateWorkUnitCommand command)
        => ValidateScoped(
            command.WorkCenterId,
            command.Code,
            command.Name,
            command.Status,
            "Work center",
            command.Description);
}
