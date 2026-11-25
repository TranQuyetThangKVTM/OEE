using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Operations;
using Oee.Application.Organizations.Areas.Common;
using Oee.Domain.Common;
using Oee.Domain.Entities.Organization;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Organizations.Areas.Create;

/// <summary>Điều phối thao tác CreateArea trong cùng scope lưu dữ liệu.</summary>
internal sealed class CreateAreaHandler(AreaContext context)
    : IRequestHandler<CreateAreaCommand, MasterMutationResult>
{
    public async Task<MasterMutationResult> HandleAsync(
        CreateAreaCommand command,
        CancellationToken cancellationToken)
    {
        context.Context.EnsureAuthorized();

        var values = CreateAreaValidator.ValidateArea(command);
        await MasterDataCommandContext.EnsureActiveAsync(
            () => context.store.AnySitesAsync(x => x.Id == values.ParentId && x.Status == RecordStatusEnum.Active, cancellationToken),
            values.ParentId,
            "Site");
        await context.EnsureUniqueAreaCodeAsync(values.ParentId, values.Code, null, cancellationToken);

        var entity = AreaEntity.Create(
            siteId: values.ParentId,
            code: values.Code,
            name: values.Name,
            description: values.Description,
            status: values.Status);

        context.store.AddArea(entity);
        await context.Context.SaveCreateAsync("Area", cancellationToken);

        return Result(entity);
    }
}
