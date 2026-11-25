namespace Oee.Tests.UseCases;
public interface IOpcUaWriteBackScenario
{
    Task WriteAsync(CancellationToken cancellationToken);
}
