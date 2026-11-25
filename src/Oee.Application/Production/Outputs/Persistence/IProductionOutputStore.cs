using Oee.Domain.Entities.Production;

namespace Oee.Application.Operations;

public interface IProductionOutputStore
{
    Task<decimal?> ReadEffectiveSpeedAsync(
        Guid unitId,
        Guid productId,
        DateOnly effectiveDate,
        CancellationToken ct);

    void Add(ProductionOutputEntity output);
}
