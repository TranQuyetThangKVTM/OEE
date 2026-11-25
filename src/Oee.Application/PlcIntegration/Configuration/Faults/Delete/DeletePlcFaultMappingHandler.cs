using Oee.Application.Common;
using Oee.Application.PlcIntegration.Configuration.Faults.Common;
using static Oee.Application.PlcIntegration.PlcConfigurationValidation;

namespace Oee.Application.PlcIntegration.Configuration.Faults.Delete;

/// <summary>Điều phối thao tác DeletePlcFaultMapping trong cùng scope lưu dữ liệu.</summary>
internal sealed class DeletePlcFaultMappingHandler(PlcFaultContext context)
    : ICommandHandler<DeletePlcFaultMappingCommand>
{
    public async Task HandleAsync(
        DeletePlcFaultMappingCommand command,
        CancellationToken cancellationToken)
    {
        context.Context.EnsureAuthorized();

        var id = command.Id;
        var rowVersion = command.RowVersion;
        var entity = await context.store.FindFaultAsync(id, cancellationToken) ?? throw Missing("Fault mapping");
        context.Context.ApplyConcurrency(entity, rowVersion);
        entity.Delete(context.Context.Clock.UtcNow);
        await context.Context.UnitOfWork.SaveChangesAsync(cancellationToken);
    }
}
