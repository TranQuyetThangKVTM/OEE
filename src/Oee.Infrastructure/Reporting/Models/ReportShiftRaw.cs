
namespace Oee.Infrastructure.Reporting;

internal sealed record ReportShiftRaw(Guid Id, string Code, string Name, TimeOnly Start, TimeOnly End);
