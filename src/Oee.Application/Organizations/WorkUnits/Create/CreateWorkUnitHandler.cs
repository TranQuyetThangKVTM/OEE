using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Operations;
using Oee.Application.Organizations.WorkUnits.Common;
using Oee.Domain.Common;
using Oee.Domain.Entities.Organization;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Organizations.WorkUnits.Create;

/// <summary>Điều phối thao tác CreateWorkUnit trong cùng scope lưu dữ liệu.</summary>
internal sealed class CreateWorkUnitHandler(WorkUnitContext context)
    : IRequestHandler<CreateWorkUnitCommand, MasterMutationResult>
{
    public async Task<MasterMutationResult> HandleAsync(
        CreateWorkUnitCommand command,
        CancellationToken cancellationToken)
    {
        context.Context.EnsureAuthorized();

        var values = CreateWorkUnitValidator.ValidateWorkUnit(command);
        await MasterDataCommandContext.EnsureActiveAsync(
            () => context.store.AnyWorkCentersAsync(
                x => x.Id == values.ParentId && x.Status == RecordStatusEnum.Active,
                cancellationToken),
            values.ParentId,
            "Work center");
        await context.EnsureUniqueWorkUnitCodeAsync(values.ParentId, values.Code, null, cancellationToken);

        var entity = WorkUnitEntity.Create(
            workCenterId: values.ParentId,
            code: values.Code,
            name: values.Name,
            description: values.Description,
            status: values.Status);

        context.store.AddWorkUnit(entity);
        await context.Context.SaveCreateAsync("Work unit", cancellationToken);

        return Result(entity);
    }
}
