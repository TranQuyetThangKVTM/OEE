using Oee.Application.Common;
using Oee.Application.PlcIntegration.Configuration.Faults.Common;
using static Oee.Application.PlcIntegration.PlcConfigurationValidation;

namespace Oee.Application.PlcIntegration.Configuration.Faults.Update;

/// <summary>Điều phối thao tác UpdatePlcFaultMapping trong cùng scope lưu dữ liệu.</summary>
internal sealed class UpdatePlcFaultMappingHandler(PlcFaultContext context)
    : ICommandHandler<UpdatePlcFaultMappingCommand>
{
    public async Task HandleAsync(
        UpdatePlcFaultMappingCommand command,
        CancellationToken cancellationToken)
    {
        context.Context.EnsureAuthorized();

        var id = command.Id;
        var request = command.Request;
        var values = PlcFaultValidator.ValidateFault(request);
        var entity = await context.store.FindFaultAsync(id, cancellationToken) ?? throw Missing("Fault mapping");
        context.Context.ApplyConcurrency(entity, RequiredVersion(request.RowVersion));
        await context.ValidateFaultReferencesAndUniquenessAsync(values, id, cancellationToken);
        entity.Update(values.FaultCode, values.ReasonId, values.WorkUnitId, values.Description, values.Active);
        context.Context.Touch(entity);
        await context.Context.UnitOfWork.SaveChangesAsync(cancellationToken);
    }
}
