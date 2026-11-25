namespace Oee.Application.Production.LineOff;

public interface ILineOffScopeStore
{
    Task<LineOffScope?> FindAsync(Guid workCenterId, CancellationToken cancellationToken);
    Task<bool> HasProductionAsync(Guid fillerId, DateTimeOffset start, DateTimeOffset until, CancellationToken cancellationToken);
}
