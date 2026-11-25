using Oee.Application.Common;
using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.PlcIntegration;

namespace Oee.Application.PlcIntegration.Configuration.Devices.Queries;

/// <summary>Dữ liệu yêu cầu cho thao tác ListPlcDeviceMappings.</summary>
public sealed record ListPlcDeviceMappingsQuery(PlcListQuery Query)
    : IQuery<PagedResult<PlcDeviceMappingItem>>;
