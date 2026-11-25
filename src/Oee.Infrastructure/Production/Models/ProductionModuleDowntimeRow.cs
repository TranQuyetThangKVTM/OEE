using Oee.Domain.Common;

namespace Oee.Infrastructure.Operations;

internal sealed record ProductionModuleDowntimeRow(Guid Id, Guid WorkUnitId, string WorkUnit, Guid ReasonId, string ReasonCode,
    string Reason, DateTimeOffset StartAt, DateTimeOffset? EndAt, DowntimeCategoryEnum Category, DowntimeKindEnum Kind,
    DataSourceEnum Source, string? Note, Guid? OutputId, DateTimeOffset? UpdatedAt);
