using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Organizations.Sites.Common;
using static Oee.Application.Operations.MasterDataValidation;
using Oee.Application.Organizations.Sites.Create;

namespace Oee.Application.Organizations.Sites.Update;

/// <summary>Điều phối thao tác UpdateSite trong cùng scope lưu dữ liệu.</summary>
internal sealed class UpdateSiteHandler(SiteContext context)
    : IRequestHandler<UpdateSiteCommand, MasterMutationResult>
{
    public async Task<MasterMutationResult> HandleAsync(
        UpdateSiteCommand command,
        CancellationToken cancellationToken)
    {
        context.Context.EnsureAuthorized();

        var entity = await context.store.FindSiteAsync(command.Id, cancellationToken) ?? throw Missing("Site");
        var code = command.Code is null ? entity.Code : Required(command.Code, 32, "Code");
        await context.EnsureUniqueSiteCodeAsync(entity.EnterpriseId, code, entity.Id, cancellationToken);
        var zone = CreateSiteValidator.ValidateTimeZone(command.TimeZoneId);
        context.Context.ApplyConcurrency(entity, command.RowVersion);
        entity.Update(code, command.Name, zone, command.Description);
        context.Context.Touch(entity);
        await context.Context.UnitOfWork.SaveChangesAsync(cancellationToken);

        return Result(entity);
    }
}
