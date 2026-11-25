using Oee.Application.Modules;

namespace Oee.Application.Contracts.Modules;

public sealed record LineOeeSummary(
    DateOnly Date,
    string? Line,
    string? Machine,
    string? TimeZoneId,
    DashboardRollupResult? Day,
    DashboardRollupResult? Month,
    DateTimeOffset? CalculatedAt,
    string? Message = null,
    Guid? WorkCenterId = null,
    decimal DayOffMinutes = 0,
    decimal MonthOffMinutes = 0,
    decimal DayMissingMinutes = 0,
    decimal MonthMissingMinutes = 0);
