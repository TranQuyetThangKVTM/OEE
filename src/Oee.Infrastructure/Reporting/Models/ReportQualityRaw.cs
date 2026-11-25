
namespace Oee.Infrastructure.Reporting;

internal sealed record ReportQualityRaw(Guid ProductId, string Code, string Name, DateTimeOffset At, decimal Total, decimal Defect);
