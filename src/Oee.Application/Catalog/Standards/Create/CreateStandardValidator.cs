using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Catalog.Standards.Create;

/// <summary>Kiểm tra và chuẩn hóa đầu vào, không đọc hoặc ghi database.</summary>
internal static class CreateStandardValidator
{
    internal static (
        Guid WorkUnitId,
        Guid ProductId,
        decimal Speed,
        string Uom,
        DateOnly From,
        DateOnly? To,
        string? ApprovedBy)
        ValidateStandard(CreateStandardCommand command)
    {
        if (command.WorkUnitId == Guid.Empty || command.ProductId == Guid.Empty || command.SpeedPerMinute <= 0
            || command.EffectiveTo < command.EffectiveFrom)
            throw new ArgumentException("Standard scope, speed and effective interval are invalid.");

        return (
            command.WorkUnitId,
            command.ProductId,
            command.SpeedPerMinute,
            Required(command.Uom, 16, "UOM"),
            command.EffectiveFrom,
            command.EffectiveTo,
            Optional(command.ApprovedBy, 120, "Approved by"));
    }
}
