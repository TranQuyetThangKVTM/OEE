namespace Oee.Application.Common;

/// <summary>Transaction do use case điều phối; adapter chịu trách nhiệm commit/rollback và giải phóng.</summary>
public interface IApplicationTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);

    Task RollbackAsync(CancellationToken cancellationToken);
}
