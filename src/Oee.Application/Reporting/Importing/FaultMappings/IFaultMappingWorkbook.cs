namespace Oee.Application.Reporting;

public interface IFaultMappingWorkbook
{
    byte[] CreateTemplate();

    Task<FaultMappingWorkbookData> ReadAsync(
        Stream source,
        long length,
        CancellationToken ct);
}
