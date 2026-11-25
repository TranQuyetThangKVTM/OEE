namespace Oee.Application.Production.Automation;

public interface IAutomaticShiftReportFiles
{
    Task SaveAsync(
        Guid id,
        byte[] content,
        CancellationToken ct);

    Task<byte[]> ReadAsync(Guid id, CancellationToken ct);

    Task DeleteAsync(Guid id, CancellationToken ct);
}
