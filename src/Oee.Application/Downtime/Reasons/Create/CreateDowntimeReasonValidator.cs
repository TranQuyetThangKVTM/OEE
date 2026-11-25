using Oee.Domain.Common;
using Oee.Domain.Entities.Organization;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Downtime.Reasons.Create;

/// <summary>Kiểm tra và chuẩn hóa đầu vào, không đọc hoặc ghi database.</summary>
internal static class CreateDowntimeReasonValidator
{
    internal static (
        string Code,
        string Name,
        DowntimeCategoryEnum Category,
        DowntimeKindEnum Kind,
        string? Description,
        RecordStatusEnum Status)
        ValidateReason(CreateDowntimeReasonCommand command)
    {
        if (!Enum.TryParse<DowntimeCategoryEnum>(command.Category, true, out var category)
            || !Enum.TryParse<DowntimeKindEnum>(command.Kind, true, out var kind))
            throw new ArgumentException("Downtime category or kind is invalid.");
        DowntimeReasonEntity.ValidateClassification(category, kind);

        return (
            Required(command.Code, 32, "Reason code"),
            Required(command.Name, 200, "Reason name"),
            category,
            kind,
            Optional(command.Description, 500, "Description"),
            Status(command.Status));
    }
}
