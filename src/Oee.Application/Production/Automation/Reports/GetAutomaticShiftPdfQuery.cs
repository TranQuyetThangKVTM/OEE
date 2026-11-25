using Oee.Application.Common;

namespace Oee.Application.Production.Automation;

public sealed record GetAutomaticShiftPdfQuery(Guid Id) : IRequest<AutomaticShiftPdf>;
