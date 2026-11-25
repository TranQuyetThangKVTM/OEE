
namespace Oee.Infrastructure.Operations;

internal sealed record LiveOeePeriodBounds(DateOnly Date, int WeekNumber, DateTimeOffset DayStart, DateTimeOffset WeekStart,
    DateTimeOffset MonthStart, LiveOeeCurrentShiftRow? Shift);
