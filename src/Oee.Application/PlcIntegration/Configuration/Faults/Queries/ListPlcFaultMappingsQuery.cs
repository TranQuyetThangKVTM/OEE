using Oee.Application.Common;
using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.PlcIntegration;

namespace Oee.Application.PlcIntegration.Configuration.Faults.Queries;

/// <summary>Dữ liệu yêu cầu cho thao tác ListPlcFaultMappings.</summary>
public sealed record ListPlcFaultMappingsQuery(PlcListQuery Query) : IQuery<PagedResult<PlcFaultMappingItem>>;
