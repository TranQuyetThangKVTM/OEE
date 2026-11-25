namespace Oee.Application.Common;

/// <summary>Thực hiện đúng một command hoặc query có kết quả.</summary>
public interface IRequestHandler<in TRequest, TResult>
where TRequest : IRequest<TResult>
{
    Task<TResult> HandleAsync(TRequest request, CancellationToken cancellationToken);
}
