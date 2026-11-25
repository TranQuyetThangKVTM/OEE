using Oee.Application.Common;
using Oee.Application.Contracts.Reporting;

namespace Oee.Application.Reporting.Importing.FaultMappings.Preview;

/// <summary>Dữ liệu yêu cầu cho thao tác PreviewFaultMappings.</summary>
public sealed record PreviewFaultMappingsQuery(
    Stream Xlsx,
    long Length) : IQuery<FaultMappingImportPreview>;
