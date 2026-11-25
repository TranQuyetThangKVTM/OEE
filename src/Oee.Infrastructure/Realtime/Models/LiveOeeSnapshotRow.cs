
namespace Oee.Infrastructure.Operations;

internal sealed record LiveOeeSnapshotRow(Guid WorkUnitId, decimal Oee, decimal Availability, decimal Performance, decimal Quality, decimal LoadingMinutes, decimal UtilizationMinutes, decimal Total, decimal Defect, DateTimeOffset CalculatedAt, string? AnomalyFlags);
