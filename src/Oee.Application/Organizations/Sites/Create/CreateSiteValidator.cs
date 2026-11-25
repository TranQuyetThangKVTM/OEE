using Oee.Domain.Common;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Organizations.Sites.Create;

/// <summary>Kiểm tra và chuẩn hóa đầu vào, không đọc hoặc ghi database.</summary>
internal static class CreateSiteValidator
{
    internal static (
        Guid ParentId,
        string Code,
        string Name,
        string TimeZone,
        RecordStatusEnum Status,
        string? Description)
        ValidateSite(CreateSiteCommand command)
    {
        if (command.EnterpriseId == Guid.Empty)
            throw new ArgumentException("Enterprise is required.");

        return (
            command.EnterpriseId,
            Required(command.Code, 32, "Site code"),
            Required(command.Name, 200, "Site name"),
            CreateSiteValidator.ValidateTimeZone(command.TimeZoneId),
            Status(command.Status),
            Optional(command.Description, 500, "Description"));
    }

    internal static string ValidateTimeZone(string value)
    {
        var zone = Required(value, 100, "Time zone");

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(zone);
        }
        catch (TimeZoneNotFoundException)
        {
            throw new ArgumentException("Time zone is not available on this host.");
        }
        catch (InvalidTimeZoneException)
        {
            throw new ArgumentException("Time zone is invalid.");
        }

        return zone;
    }
}
