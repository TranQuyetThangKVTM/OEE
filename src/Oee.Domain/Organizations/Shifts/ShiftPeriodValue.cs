namespace Oee.Domain.Organization;

/// <summary>Một instance ca cụ thể đã được gắn ngày local và offset timezone.</summary>
public sealed record ShiftPeriodValue(DateTimeOffset Start, DateTimeOffset End, DateOnly LocalDate);
