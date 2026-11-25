using Microsoft.EntityFrameworkCore;
using Oee.Application.Operations;
using Oee.Domain.Entities.Production;
using Oee.Infrastructure.Persistence;

namespace Oee.Infrastructure.Operations;

public sealed class EfProductionOutputStore(EfApplicationUnitOfWork unitOfWork) : IProductionOutputStore
{
    private OeeDbContext Db => unitOfWork.Db;
    public Task<decimal?> ReadEffectiveSpeedAsync(Guid unitId, Guid productId, DateOnly effectiveDate, CancellationToken ct) => Db.WorkUnitStandards.Where(x => x.WorkUnitId == unitId && x.ProductId == productId && x.EffectiveFrom <= effectiveDate && (!x.EffectiveTo.HasValue || x.EffectiveTo >= effectiveDate)).OrderByDescending(x => x.EffectiveFrom).Select(x => (decimal?)x.StandardSpeedPerMinute).FirstOrDefaultAsync(ct);
    public void Add(ProductionOutputEntity output) => Db.ProductionOutputs.Add(output);
}
