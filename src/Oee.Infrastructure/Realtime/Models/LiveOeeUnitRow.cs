using Oee.Domain.Common;

namespace Oee.Infrastructure.Operations;

internal sealed record LiveOeeUnitRow(Guid Id, string Code, string Name, OperationalStateEnum OperationalState, Guid SiteId, string TimeZoneId);
