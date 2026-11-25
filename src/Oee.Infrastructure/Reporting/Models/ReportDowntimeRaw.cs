
namespace Oee.Infrastructure.Reporting;

internal sealed record ReportDowntimeRaw(Guid Id, Guid WorkUnitId, string WorkUnit, Guid ReasonId, string ReasonCode,
    string Reason, DateTimeOffset Start, DateTimeOffset? End, decimal Minutes, string Category, string Kind,
    string Source, string? Note);
