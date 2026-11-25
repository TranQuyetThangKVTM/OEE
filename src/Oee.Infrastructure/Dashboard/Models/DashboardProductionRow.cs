
namespace Oee.Infrastructure.Operations;

internal sealed record DashboardProductionRow(Guid WorkUnitId, DateTimeOffset PeriodStart, decimal Total, decimal Defect, decimal IdealRunMinutes);
