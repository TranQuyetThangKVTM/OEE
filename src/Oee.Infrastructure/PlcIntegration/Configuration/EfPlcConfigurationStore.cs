using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Oee.Application.PlcIntegration;
using Oee.Domain.PlcIntegration;
using Oee.Infrastructure.Persistence;

namespace Oee.Infrastructure.PlcIntegration;

public sealed class EfPlcConfigurationStore(EfApplicationUnitOfWork unitOfWork) : IPlcConfigurationStore
{
    private OeeDbContext Db => unitOfWork.Db;
    public Task<OpcUaConnectionEntity?> FindConnectionAsync(Guid id, bool tracking, CancellationToken ct) => (tracking ? Db.OpcUaConnections : Db.OpcUaConnections.AsNoTracking()).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<PlcDeviceMappingEntity?> FindDeviceAsync(Guid id, CancellationToken ct) => Db.PlcDeviceMappings.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<PlcFaultCodeMappingEntity?> FindFaultAsync(Guid id, CancellationToken ct) => Db.PlcFaultCodeMappings.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<OpcUaTagMappingEntity[]> ReadTagsAsync(Guid deviceId, CancellationToken ct) => Db.OpcUaTagMappings.Where(x => x.PlcDeviceMappingId == deviceId).ToArrayAsync(ct);
    public Task<bool> AnyConnectionsAsync(Expression<Func<OpcUaConnectionEntity, bool>> predicate, CancellationToken ct) => Db.OpcUaConnections.AnyAsync(predicate, ct);
    public Task<bool> AnyDevicesAsync(Expression<Func<PlcDeviceMappingEntity, bool>> predicate, CancellationToken ct) => Db.PlcDeviceMappings.AnyAsync(predicate, ct);
    public Task<bool> AnyFaultsAsync(Expression<Func<PlcFaultCodeMappingEntity, bool>> predicate, CancellationToken ct) => Db.PlcFaultCodeMappings.AnyAsync(predicate, ct);
    public void AddConnection(OpcUaConnectionEntity connection) => Db.OpcUaConnections.Add(connection);
    public void AddDevice(PlcDeviceMappingEntity device) => Db.PlcDeviceMappings.Add(device);
    public void AddTag(OpcUaTagMappingEntity tag) => Db.OpcUaTagMappings.Add(tag);
    public void AddFault(PlcFaultCodeMappingEntity fault) => Db.PlcFaultCodeMappings.Add(fault);
}
