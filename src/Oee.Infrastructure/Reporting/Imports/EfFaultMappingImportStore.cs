using Microsoft.EntityFrameworkCore;
using Oee.Application.Reporting;
using Oee.Domain.PlcIntegration;
using Oee.Infrastructure.Persistence;

namespace Oee.Infrastructure.Reporting;

public sealed class EfFaultMappingImportStore(EfApplicationUnitOfWork unitOfWork) : IFaultMappingImportStore
{
    private OeeDbContext Db => unitOfWork.Db;
    public Task<Dictionary<string, Guid>> ReadWorkUnitIdsAsync(bool tracking, CancellationToken ct) =>
        (tracking ? Db.WorkUnits : Db.WorkUnits.AsNoTracking()).ToDictionaryAsync(x => x.Code, x => x.Id, StringComparer.OrdinalIgnoreCase, ct);
    public Task<Dictionary<string, Guid>> ReadReasonIdsAsync(bool tracking, CancellationToken ct) =>
        (tracking ? Db.DowntimeReasons : Db.DowntimeReasons.AsNoTracking()).ToDictionaryAsync(x => x.Code, x => x.Id, StringComparer.OrdinalIgnoreCase, ct);
    public Task<PlcFaultCodeMappingEntity?> FindIncludingDeletedAsync(Guid? unitId, string faultCode, CancellationToken ct) => Db.PlcFaultCodeMappings.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.WorkUnitId == unitId && x.FaultCode == faultCode, ct);
    public void Add(PlcFaultCodeMappingEntity mapping) => Db.PlcFaultCodeMappings.Add(mapping);
}
