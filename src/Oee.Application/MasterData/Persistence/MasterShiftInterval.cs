namespace Oee.Application.Operations;

public sealed record MasterShiftInterval(
    TimeOnly StartTime,
    TimeOnly EndTime);
