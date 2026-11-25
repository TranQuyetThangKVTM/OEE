using Oee.Domain.Common;

namespace Oee.Infrastructure.Operations;

internal sealed record LiveOeeRuntimeRow(Guid WorkUnitId, Guid DeviceId, ConnectivityStateEnum ConnectivityState, OperationalStateEnum OperationalState, DateTimeOffset? LastDataAt, DateTimeOffset? LastTransitionAt);
