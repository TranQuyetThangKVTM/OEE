using Oee.Domain.PlcIntegration;

namespace Oee.Application.Reporting;

public interface IFaultMappingImportStore
{
    Task<Dictionary<string, Guid>> ReadWorkUnitIdsAsync(bool tracking, CancellationToken ct);

    Task<Dictionary<string, Guid>> ReadReasonIdsAsync(bool tracking, CancellationToken ct);

    Task<PlcFaultCodeMappingEntity?> FindIncludingDeletedAsync(
        Guid? unitId,
        string faultCode,
        CancellationToken ct);

    void Add(PlcFaultCodeMappingEntity mapping);
}
