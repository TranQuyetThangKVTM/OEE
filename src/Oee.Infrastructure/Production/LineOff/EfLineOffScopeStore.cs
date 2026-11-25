using Microsoft.EntityFrameworkCore;
using Oee.Application.Production.LineOff;
using Oee.Domain.Common;
using Oee.Infrastructure.Persistence;

namespace Oee.Infrastructure.Production.LineOff;

public sealed class EfLineOffScopeStore(OeeDbContext db) : ILineOffScopeStore
{
    public async Task<LineOffScope?> FindAsync(Guid workCenterId, CancellationToken cancellationToken)
    {
        var candidates = await db.WorkUnits.AsNoTracking()
            .Where(x => x.WorkCenterId == workCenterId && x.Name.Trim() == "Filler" && x.Status == RecordStatusEnum.Active
                && x.WorkCenter.Status == RecordStatusEnum.Active && x.WorkCenter.Area.Status == RecordStatusEnum.Active
                && x.WorkCenter.Area.Site.Status == RecordStatusEnum.Active)
            .Select(x => new LineOffScope(x.WorkCenterId, x.Id, x.WorkCenter.Area.Site.TimeZoneId))
            .Take(2).ToArrayAsync(cancellationToken);
        return candidates.Length == 1 ? candidates[0] : null;
    }

    public Task<bool> HasProductionAsync(Guid fillerId, DateTimeOffset start, DateTimeOffset until, CancellationToken cancellationToken)
        => db.ProductionOutputs.AsNoTracking().AnyAsync(x => x.WorkUnitId == fillerId && x.TotalProducedQuantity > 0
            && x.PeriodEnd > start && x.PeriodEnd <= until, cancellationToken);
}
