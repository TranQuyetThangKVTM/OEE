using System.Data;
using Oee.Application.Common;
using Oee.Application.PlcIntegration.Configuration.Devices.Common;
using Oee.Domain.PlcIntegration;

namespace Oee.Application.PlcIntegration.Configuration.Devices.Create;

/// <summary>Điều phối thao tác CreatePlcDeviceConfiguration trong cùng scope lưu dữ liệu.</summary>
internal sealed class CreatePlcDeviceConfigurationHandler(PlcDeviceContext context)
    : IRequestHandler<CreatePlcDeviceConfigurationCommand, Guid>
{
    public async Task<Guid> HandleAsync(
        CreatePlcDeviceConfigurationCommand command,
        CancellationToken cancellationToken)
    {
        context.Context.EnsureAuthorized();

        var request = command.Request;
        var values = PlcDeviceValidator.ValidateDevice(request);
        await using var transaction = await context.Context.UnitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await context.ValidateDeviceReferencesAndUniquenessAsync(values, null, cancellationToken);

        var entity = PlcDeviceMappingEntity.Create(
            values.ConnectionId,
            values.WorkUnitId,
            values.ExternalId,
            values.CounterMode,
            values.FaultFormat,
            values.Active,
            values.ProductId,
            values.Debounce,
            values.ZeroNoFault,
            values.Note);

        context.store.AddDevice(entity);

        foreach (var tag in values.Tags)
            context.store.AddTag(PlcDeviceContext.ToEntity(entity.Id, tag));
        await context.Context.UnitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return entity.Id;
    }
}
