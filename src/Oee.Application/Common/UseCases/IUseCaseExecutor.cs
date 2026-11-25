namespace Oee.Application.Common;

/// <summary>Định tuyến yêu cầu có kiểu tới handler được đăng ký trong cùng scope.</summary>
public interface IUseCaseExecutor
{
    Task<TResult> ExecuteAsync<TResult>(
        IRequest<TResult> request,
        CancellationToken cancellationToken = default);

    Task ExecuteAsync(ICommand command, CancellationToken cancellationToken = default);
}
