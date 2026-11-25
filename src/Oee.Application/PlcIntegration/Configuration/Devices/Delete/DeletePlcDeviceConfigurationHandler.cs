using Oee.Application.Common;
using Oee.Application.PlcIntegration.Configuration.Devices.Common;
using static Oee.Application.PlcIntegration.PlcConfigurationValidation;

namespace Oee.Application.PlcIntegration.Configuration.Devices.Delete;

/// <summary>Điều phối thao tác DeletePlcDeviceConfiguration trong cùng scope lưu dữ liệu.</summary>
internal sealed class DeletePlcDeviceConfigurationHandler(PlcDeviceContext context)
    : ICommandHandler<DeletePlcDeviceConfigurationCommand>
{
    public async Task HandleAsync(
        DeletePlcDeviceConfigurationCommand command,
        CancellationToken cancellationToken)
    {
        context.Context.EnsureAuthorized();

        var id = command.Id;
        var rowVersion = command.RowVersion;
        var entity = await context.store.FindDeviceAsync(id, cancellationToken) ?? throw Missing("PLC device mapping");
        context.Context.ApplyConcurrency(entity, rowVersion);
        entity.Delete(context.Context.Clock.UtcNow);
        var tags = await context.store.ReadTagsAsync(id, cancellationToken);

        foreach (var tag in tags)
        {
            tag.SoftDelete(context.Context.Clock.UtcNow);
        }

        await context.Context.UnitOfWork.SaveChangesAsync(cancellationToken);
    }
}
