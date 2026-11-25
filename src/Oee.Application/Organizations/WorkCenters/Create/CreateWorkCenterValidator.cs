using Oee.Domain.Common;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Organizations.WorkCenters.Create;

/// <summary>Kiểm tra và chuẩn hóa đầu vào, không đọc hoặc ghi database.</summary>
internal static class CreateWorkCenterValidator
{
    internal static (
        Guid ParentId,
        string Code,
        string Name,
        RecordStatusEnum Status,
        string? Description)
        ValidateGroup(CreateWorkCenterCommand command)
        => ValidateScoped(command.AreaId, command.Code, command.Name, command.Status, "Area", command.Description);
}
