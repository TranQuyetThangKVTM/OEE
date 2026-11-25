
namespace Oee.Infrastructure.Operations;

internal sealed record LiveOeePlanRow(Guid Id, Guid WorkUnitId, string Code, Guid ProductId, string ProductCode, decimal Target);
