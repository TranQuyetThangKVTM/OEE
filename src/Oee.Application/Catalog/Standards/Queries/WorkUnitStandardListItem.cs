namespace Oee.Application.Contracts.Modules;

/// <summary>Dòng định mức tốc độ; speed/phút là dữ liệu lưu, speed/giờ là projection hiển thị.</summary>
public sealed record WorkUnitStandardListItem(
    Guid Id,
    Guid WorkUnitId,
    string WorkUnit,
    Guid ProductId,
    string Product,
    decimal SpeedPerMinute,
    decimal SpeedPerHour,
    string Uom,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string? ApprovedBy,
    bool IsEffective,
    DateTimeOffset UpdatedAt,
    string RowVersion);
