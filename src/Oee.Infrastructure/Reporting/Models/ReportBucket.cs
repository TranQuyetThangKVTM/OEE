
namespace Oee.Infrastructure.Reporting;

internal sealed class ReportBucket(string label, string? code, Guid workUnitId, string workUnit)
{
    public string Label { get; } = label; public string? Code { get; } = code;
    public Guid WorkUnitId { get; } = workUnitId; public string WorkUnit { get; } = workUnit;
    public List<ReportSnapshotRaw> Snapshots { get; } = []; public List<ReportOutputRaw> Outputs { get; } = [];
}
