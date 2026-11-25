using Oee.Application.Common;
using Oee.Application.Contracts.PlcIntegration;

namespace Oee.Application.PlcIntegration.Configuration.Overview.Queries;

/// <summary>Dữ liệu yêu cầu cho thao tác GetPlcLookups.</summary>
public sealed record GetPlcLookupsQuery() : IQuery<PlcConfigurationLookups>;
