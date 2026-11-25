namespace Oee.Application.Production.Automation;

/// <summary>Một lịch D+1 cho hệ thống; thời gian chạy dùng múi giờ nhà máy của mẫu.</summary>
public sealed record ProductionPlanSchedule(
    Guid Revision,
    bool Enabled,
    Guid SamplePlanId,
    string SamplePlanCode,
    TimeOnly RunAt,
    string TimeZoneId,
    DateOnly FirstRunDate,
    string? UpdatedById,
    string UpdatedBy,
    DateTimeOffset UpdatedAt,
    DateOnly? LastRunDate = null,
    DateTimeOffset? LastAttemptAt = null,
    PlanAutomationResult? LastResult = null,
    string? LastError = null,
    bool PdfEnabled = false,
    int PdfDelayMinutes = 5,
    DateTimeOffset? PdfEnabledAt = null,
    IReadOnlyList<GeneratedShiftReport>? Reports = null,
    IReadOnlyList<ShiftReportCursor>? PdfCursors = null,
    DateTimeOffset? PdfLastAttemptAt = null,
    string? PdfLastError = null);
