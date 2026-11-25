using Oee.Application.Common;
using Oee.Application.Contracts.PlcIntegration;

namespace Oee.Application.PlcIntegration.Configuration.Overview.Queries;

/// <summary>Điều phối thao tác GetPlcLookups trong cùng scope lưu dữ liệu.</summary>
internal sealed class GetPlcLookupsHandler(IPlcConfigurationReadStore reads)
    : IRequestHandler<GetPlcLookupsQuery, PlcConfigurationLookups>
{
    public Task<PlcConfigurationLookups> HandleAsync(
        GetPlcLookupsQuery command,
        CancellationToken cancellationToken)
    {
        return reads.LookupsAsync(cancellationToken);
    }
}
