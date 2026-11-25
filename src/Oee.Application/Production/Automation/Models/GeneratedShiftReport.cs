namespace Oee.Application.Production.Automation;

public sealed record GeneratedShiftReport(
    Guid Id,
    Guid ShiftId,
    string ShiftCode,
    DateOnly OperationalDate,
    DateTimeOffset ShiftEnd,
    DateTimeOffset GeneratedAt,
    string FileName,
    bool HasData);
