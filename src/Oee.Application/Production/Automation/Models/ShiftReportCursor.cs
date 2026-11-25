namespace Oee.Application.Production.Automation;

public sealed record ShiftReportCursor(
    Guid ShiftId,
    DateTimeOffset LastCompletedEnd);
