using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Operations;
using Oee.Application.Organizations.WorkCenters.Common;
using Oee.Domain.Common;
using Oee.Domain.Entities.Organization;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Organizations.WorkCenters.Create;

/// <summary>Điều phối thao tác CreateWorkCenter trong cùng scope lưu dữ liệu.</summary>
internal sealed class CreateWorkCenterHandler(WorkCenterContext context)
    : IRequestHandler<CreateWorkCenterCommand, MasterMutationResult>
{
    public async Task<MasterMutationResult> HandleAsync(
        CreateWorkCenterCommand command,
        CancellationToken cancellationToken)
    {
        context.Context.EnsureAuthorized();

        var values = CreateWorkCenterValidator.ValidateGroup(command);
        await MasterDataCommandContext.EnsureActiveAsync(
            () => context.store.AnyAreasAsync(x => x.Id == values.ParentId && x.Status == RecordStatusEnum.Active, cancellationToken),
            values.ParentId,
            "Area");
        await context.EnsureUniqueGroupCodeAsync(values.ParentId, values.Code, null, cancellationToken);

        var entity = WorkCenterEntity.Create(
            areaId: values.ParentId,
            code: values.Code,
            name: values.Name,
            description: values.Description,
            status: values.Status);

        context.store.AddWorkCenter(entity);
        await context.Context.SaveCreateAsync("Work center", cancellationToken);

        return Result(entity);
    }
}
