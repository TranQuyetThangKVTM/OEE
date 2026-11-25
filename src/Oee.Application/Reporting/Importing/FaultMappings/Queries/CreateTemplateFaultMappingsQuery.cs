using Oee.Application.Common;

namespace Oee.Application.Reporting.Importing.FaultMappings.Queries;

/// <summary>Dữ liệu yêu cầu cho thao tác CreateTemplateFaultMappings.</summary>
public sealed record CreateTemplateFaultMappingsQuery() : IQuery<byte[]>;
