using Oee.Application.Common;
using Oee.Application.Contracts.Reporting;

namespace Oee.Application.Reporting.Importing.FaultMappings.Import;

/// <summary>Dữ liệu yêu cầu cho thao tác ImportFaultMappings.</summary>
public sealed record ImportFaultMappingsCommand(
    Stream Xlsx,
    long Length)
    : ICommand<FaultMappingImportResult>;
