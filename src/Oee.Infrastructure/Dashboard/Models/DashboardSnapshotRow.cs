
namespace Oee.Infrastructure.Operations;

internal sealed record DashboardSnapshotRow(Guid WorkUnitId, string PeriodCode, DateTimeOffset PeriodStart, DateTimeOffset PeriodEnd, decimal LoadingMinutes, decimal UtilizationMinutes, DateTimeOffset CalculatedAt, string CalculationVersion);
