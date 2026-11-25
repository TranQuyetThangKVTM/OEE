using Oee.Application.Common;

namespace Oee.Application.Production.LineOff;

public sealed record SaveLineOffPeriodCommand(
    Guid WorkCenterId,
    DateTime StartLocal,
    DateTime EndLocal,
    string? Note,
    Guid ExpectedRevision,
    Guid? Id = null) : ICommand<LineOffCalendar>;
