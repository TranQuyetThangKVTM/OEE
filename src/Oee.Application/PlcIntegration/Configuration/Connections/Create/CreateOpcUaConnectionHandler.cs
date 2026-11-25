using Oee.Application.Common;
using Oee.Application.PlcIntegration.Configuration.Connections.Common;
using Oee.Domain.PlcIntegration;

namespace Oee.Application.PlcIntegration.Configuration.Connections.Create;

/// <summary>Điều phối thao tác CreateOpcUaConnection trong cùng scope lưu dữ liệu.</summary>
internal sealed class CreateOpcUaConnectionHandler(PlcConnectionContext context)
    : IRequestHandler<CreateOpcUaConnectionCommand, Guid>
{
    public async Task<Guid> HandleAsync(
        CreateOpcUaConnectionCommand command,
        CancellationToken cancellationToken)
    {
        context.Context.EnsureAuthorized();

        var request = command.Request;
        var values = PlcConnectionValidator.ValidateConnection(request);
        await context.EnsureConnectionCodeAsync(values.Code, null, cancellationToken);

        var entity = OpcUaConnectionEntity.Create(
            values.Code,
            values.Name,
            values.Endpoint,
            values.Mode,
            values.Policy,
            values.Auth,
            values.UserName,
            values.Secret,
            values.Certificate,
            values.Active);

        context.store.AddConnection(entity);
        await context.Context.UnitOfWork.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
