using Oee.Application.Common;
using Oee.Application.PlcIntegration.Configuration.Connections.Common;
using static Oee.Application.PlcIntegration.PlcConfigurationValidation;

namespace Oee.Application.PlcIntegration.Configuration.Connections.Update;

/// <summary>Điều phối thao tác UpdateOpcUaConnection trong cùng scope lưu dữ liệu.</summary>
internal sealed class UpdateOpcUaConnectionHandler(PlcConnectionContext context)
    : ICommandHandler<UpdateOpcUaConnectionCommand>
{
    public async Task HandleAsync(
        UpdateOpcUaConnectionCommand command,
        CancellationToken cancellationToken)
    {
        context.Context.EnsureAuthorized();

        var id = command.Id;
        var request = command.Request;
        var values = PlcConnectionValidator.ValidateConnection(request);
        var entity = await context.store.FindConnectionAsync(id, true, cancellationToken)
            ?? throw Missing("OPC UA connection");
        context.Context.ApplyConcurrency(entity, RequiredVersion(request.RowVersion));
        await context.EnsureConnectionCodeAsync(values.Code, id, cancellationToken);
        entity.Update(
            values.Code,
            values.Name,
            values.Endpoint,
            values.Mode,
            values.Policy,
            values.Auth,
            values.UserName,
            values.Secret,
            values.Certificate,
            values.Active,
            clearCredentialSecret: values.ClearSecret);
        context.Context.Touch(entity);
        await context.Context.UnitOfWork.SaveChangesAsync(cancellationToken);
    }
}
