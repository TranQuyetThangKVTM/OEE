using Oee.Domain.Common;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Organizations.Shifts.Create;

/// <summary>Kiểm tra và chuẩn hóa đầu vào, không đọc hoặc ghi database.</summary>
internal static class CreateShiftValidator
{
    internal static (
        Guid SiteId,
        string Code,
        string Name,
        TimeOnly Start,
        TimeOnly End,
        RecordStatusEnum Status)
        ValidateShift(CreateShiftCommand command)
    {
        if (command.SiteId == Guid.Empty || command.StartTime == command.EndTime)
            throw new ArgumentException("Site is required and shift start/end must differ.");

        return (
            command.SiteId,
            Required(command.Code, 32, "Shift code"),
            Required(command.Name, 100, "Shift name"),
            command.StartTime,
            command.EndTime,
            Status(command.Status));
    }
}
