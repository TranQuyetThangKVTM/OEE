using Oee.Application.Common;
using Oee.Application.Reporting.Importing.FaultMappings.Common;

namespace Oee.Application.Reporting.Importing.FaultMappings.Queries;

/// <summary>Điều phối thao tác CreateTemplateFaultMappings trong cùng scope lưu dữ liệu.</summary>
internal sealed class CreateTemplateFaultMappingsHandler(FaultMappingImportContext context)
    : IRequestHandler<CreateTemplateFaultMappingsQuery, byte[]>
{
    public Task<byte[]> HandleAsync(
        CreateTemplateFaultMappingsQuery command,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(context.Workbook.CreateTemplate());
    }
}
