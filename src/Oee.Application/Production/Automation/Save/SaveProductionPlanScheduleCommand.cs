using Oee.Application.Common;

namespace Oee.Application.Production.Automation;

public sealed record SaveProductionPlanScheduleCommand(
    Guid SamplePlanId,
    TimeOnly RunAt,
    bool Enabled,
    Guid? ExpectedRevision,
    bool ExportShiftPdf = false,
    int PdfDelayMinutes = 5)
    : ICommand<ProductionPlanSchedule>;
