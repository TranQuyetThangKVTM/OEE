using Microsoft.EntityFrameworkCore;
using Oee.Application.PlcIntegration.Monitoring;
using Oee.Infrastructure.Persistence;

namespace Oee.Infrastructure.PlcIntegration;

public sealed class EfOpcTagMonitorStore(OeeDbContext db) : IOpcTagMonitorStore
{
    public async Task<IReadOnlyCollection<OpcTagMonitorMapping>> ListAsync(CancellationToken ct) =>
        await db.OpcUaTagMappings.AsNoTracking().OrderBy(tag => tag.PlcDeviceMapping.WorkUnit.Code)
            .ThenBy(tag => tag.Direction).ThenBy(tag => tag.TagRole)
            .Select(tag => new OpcTagMonitorMapping(tag.Id, tag.PlcDeviceMappingId,
                tag.PlcDeviceMapping.WorkUnit.Code, tag.PlcDeviceMapping.WorkUnit.Name, tag.PlcDeviceMapping.OpcUaConnection.Code,
                tag.NodeId, tag.TagRole, tag.Direction, tag.DataType.ToString(),
                tag.PlcDeviceMapping.IsActive && tag.PlcDeviceMapping.OpcUaConnection.IsActive)).ToArrayAsync(ct);

    public async Task<OpcTagMonitorTarget?> FindAsync(Guid tagId, CancellationToken ct)
    {
        var tag = await db.OpcUaTagMappings.AsNoTracking().Include(tag => tag.PlcDeviceMapping).ThenInclude(device => device.WorkUnit)
            .Include(tag => tag.PlcDeviceMapping).ThenInclude(device => device.OpcUaConnection).SingleOrDefaultAsync(tag => tag.Id == tagId, ct);
        if (tag is null) return null;
        var device = tag.PlcDeviceMapping;
        var connection = device.OpcUaConnection;
        var mapping = new OpcTagMonitorMapping(tag.Id, device.Id, device.WorkUnit.Code, device.WorkUnit.Name,
            connection.Code, tag.NodeId, tag.TagRole, tag.Direction, tag.DataType.ToString(), device.IsActive && connection.IsActive);
        return new(mapping, new(connection.Id, connection.EndpointUrl, connection.SecurityPolicy, connection.SecurityMode,
            connection.AuthenticationType, connection.UserName, connection.CredentialSecretReference, connection.CertificateReference));
    }
}
