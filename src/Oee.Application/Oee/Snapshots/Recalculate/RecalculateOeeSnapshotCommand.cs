using Oee.Application.Common;
using Oee.Domain.Common;

namespace Oee.Application.Oee.Snapshots.Recalculate;

/// <summary>Dữ liệu yêu cầu cho thao tác RecalculateOeeSnapshot.</summary>
public sealed record RecalculateOeeSnapshotCommand(
    Guid WorkUnitId,
    OeePeriodTypeEnum PeriodType,
    string PeriodCode,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd)
    : ICommand<Guid>;
