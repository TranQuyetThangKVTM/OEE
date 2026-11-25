using Oee.Application.Common;
using Oee.Application.PlcIntegration.Configuration.Faults.Common;
using Oee.Domain.PlcIntegration;

namespace Oee.Application.PlcIntegration.Configuration.Faults.Create;

/// <summary>Điều phối thao tác CreatePlcFaultMapping trong cùng scope lưu dữ liệu.</summary>
internal sealed class CreatePlcFaultMappingHandler(PlcFaultContext context)
    : IRequestHandler<CreatePlcFaultMappingCommand, Guid>
{
    public async Task<Guid> HandleAsync(
        CreatePlcFaultMappingCommand command,
        CancellationToken cancellationToken)
    {
        context.Context.EnsureAuthorized();

        var request = command.Request;
        var values = PlcFaultValidator.ValidateFault(request);
        await context.ValidateFaultReferencesAndUniquenessAsync(values, null, cancellationToken);

        var entity = PlcFaultCodeMappingEntity.Create(
            faultCode: values.FaultCode,
            downtimeReasonId: values.ReasonId,
            workUnitId: values.WorkUnitId,
            description: values.Description,
            isActive: values.Active);

        context.store.AddFault(entity);
        await context.Context.UnitOfWork.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
