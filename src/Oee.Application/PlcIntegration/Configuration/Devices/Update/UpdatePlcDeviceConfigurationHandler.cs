using System.Data;
using Oee.Application.Common;
using Oee.Application.PlcIntegration.Configuration.Devices.Common;
using static Oee.Application.PlcIntegration.PlcConfigurationValidation;

namespace Oee.Application.PlcIntegration.Configuration.Devices.Update;

/// <summary>Điều phối thao tác UpdatePlcDeviceConfiguration trong cùng scope lưu dữ liệu.</summary>
internal sealed class UpdatePlcDeviceConfigurationHandler(PlcDeviceContext context)
    : ICommandHandler<UpdatePlcDeviceConfigurationCommand>
{
    public async Task HandleAsync(
        UpdatePlcDeviceConfigurationCommand command,
        CancellationToken cancellationToken)
    {
        context.Context.EnsureAuthorized();

        var id = command.Id;
        var request = command.Request;
        var values = PlcDeviceValidator.ValidateDevice(request);
        await using var transaction = await context.Context.UnitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            var entity = await context.store.FindDeviceAsync(id, cancellationToken) ?? throw Missing("PLC device mapping");
            context.Context.ApplyConcurrency(entity, RequiredVersion(request.RowVersion));
            await context.ValidateDeviceReferencesAndUniquenessAsync(values, id, cancellationToken);
            entity.Update(
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
            context.Context.Touch(entity);
            var existing = await context.store.ReadTagsAsync(id, cancellationToken);
            // Chỉ thay đổi cấu hình/tag; checkpoint do luồng thu thập quản lý.
            var requestedKeys = values.Tags.Select(x => (x.Role, x.Direction)).ToHashSet();

            foreach (var removed in existing.Where(x => !requestedKeys.Contains((x.TagRole, x.Direction))))
            {
                removed.SoftDelete(context.Context.Clock.UtcNow);
            }

            foreach (var tag in values.Tags)
            {
                var current = existing.SingleOrDefault(x => x.TagRole == tag.Role && x.Direction == tag.Direction);

                if (current is null)
                    context.store.AddTag(PlcDeviceContext.ToEntity(id, tag));
                else
                {
                    current.Update(tag.NodeId, tag.DataType, tag.Required);
                    context.Context.Touch(current);
                }
            }

            await context.Context.UnitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            context.Context.UnitOfWork.DiscardChanges();
            throw;
        }
    }
}
