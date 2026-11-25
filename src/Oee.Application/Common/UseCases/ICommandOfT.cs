namespace Oee.Application.Common;

/// <summary>Yêu cầu thay đổi dữ liệu và trả kết quả.</summary>
public interface ICommand<TResult> : IRequest<TResult>;
