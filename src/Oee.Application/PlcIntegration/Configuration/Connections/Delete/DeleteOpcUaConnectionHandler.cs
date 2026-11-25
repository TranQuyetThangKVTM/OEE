using Oee.Application.Common;
using Oee.Application.PlcIntegration.Configuration.Connections.Common;
using static Oee.Application.PlcIntegration.PlcConfigurationValidation;

namespace Oee.Application.PlcIntegration.Configuration.Connections.Delete;

/// <summary>Điều phối thao tác DeleteOpcUaConnection trong cùng scope lưu dữ liệu.</summary>
internal sealed class DeleteOpcUaConnectionHandler(PlcConnectionContext context)
    : ICommandHandler<DeleteOpcUaConnectionCommand>
{
    public async Task HandleAsync(
        DeleteOpcUaConnectionCommand command,
        CancellationToken cancellationToken)
    {
        context.Context.EnsureAuthorized();

        var id = command.Id;
        var rowVersion = command.RowVersion;
        var entity = await context.store.FindConnectionAsync(id, true, cancellationToken)
            ?? throw Missing("OPC UA connection");

        if (await context.store.AnyDevicesAsync(x => x.OpcUaConnectionId == id, cancellationToken))
            throw new InvalidOperationException("Connection is referenced by a device mapping. Pause it instead of deleting it.");
        context.Context.ApplyConcurrency(entity, rowVersion);
        entity.Delete(context.clock.UtcNow);
        await context.Context.UnitOfWork.SaveChangesAsync(cancellationToken);
    }
}
