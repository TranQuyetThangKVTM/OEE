
namespace Oee.Infrastructure.Reporting;

internal sealed record ReportOutputRaw(Guid Id, Guid WorkUnitId, string WorkUnit, Guid ProductId, string Product,
    Guid? ShiftId, string? Shift, DateTimeOffset Start, DateTimeOffset End, decimal Speed, decimal Total,
    decimal Defect, decimal Unscheduled);
