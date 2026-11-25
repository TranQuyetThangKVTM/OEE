
namespace Oee.Infrastructure.Operations;

internal sealed record LiveOeeDowntimeRow(Guid WorkUnitId, DateTimeOffset StartAt, string ReasonCode, string ReasonName);
