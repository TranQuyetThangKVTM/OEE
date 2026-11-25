
namespace Oee.Infrastructure.Operations;

internal sealed record LiveOeePeriodOutputRow(Guid WorkUnitId, Guid ProductId, decimal Shift, decimal Day, decimal Week, decimal Month);
