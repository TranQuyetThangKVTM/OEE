using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Operations;
using Oee.Application.Organizations.Sites.Common;
using Oee.Domain.Common;
using Oee.Domain.Entities.Organization;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Organizations.Sites.Create;

/// <summary>Điều phối thao tác CreateSite trong cùng scope lưu dữ liệu.</summary>
internal sealed class CreateSiteHandler(SiteContext context)
    : IRequestHandler<CreateSiteCommand, MasterMutationResult>
{
    public async Task<MasterMutationResult> HandleAsync(
        CreateSiteCommand command,
        CancellationToken cancellationToken)
    {
        context.Context.EnsureAuthorized();

        var values = CreateSiteValidator.ValidateSite(command);
        await MasterDataCommandContext.EnsureActiveAsync(
            () => context.store.AnyEnterprisesAsync(
                x => x.Id == values.ParentId && x.Status == RecordStatusEnum.Active,
                cancellationToken),
            values.ParentId,
            "Enterprise");
        await context.EnsureUniqueSiteCodeAsync(values.ParentId, values.Code, null, cancellationToken);

        var entity = SiteEntity.Create(
            enterpriseId: values.ParentId,
            code: values.Code,
            name: values.Name,
            timeZoneId: values.TimeZone,
            description: values.Description,
            status: values.Status);

        context.store.AddSite(entity);
        await context.Context.SaveCreateAsync("Site", cancellationToken);

        return Result(entity);
    }
}
