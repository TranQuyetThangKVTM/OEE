using Oee.Application.Common;

namespace Oee.Application.Production.LineOff;

public sealed record DeleteLineOffPeriodCommand(Guid Id, Guid ExpectedRevision) : ICommand<LineOffCalendar>;
