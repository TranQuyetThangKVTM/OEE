namespace Oee.Tests.UseCases;
public interface IAuditRetentionScenario
{
    Task<(int DeletedCount, DateTimeOffset Cutoff)> RunAsync(CancellationToken cancellationToken);
}
