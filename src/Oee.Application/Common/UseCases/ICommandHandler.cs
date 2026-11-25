namespace Oee.Application.Common;

/// <summary>Thực hiện đúng một command không có giá trị trả về.</summary>
public interface ICommandHandler<in TCommand>
where TCommand : ICommand
{
    Task HandleAsync(TCommand command, CancellationToken cancellationToken);
}
