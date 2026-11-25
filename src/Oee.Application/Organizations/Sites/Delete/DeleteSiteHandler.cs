using Oee.Application.Common;
using Oee.Application.Organizations.Sites.Common;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Organizations.Sites.Delete;

/// <summary>Điều phối thao tác DeleteSite trong cùng scope lưu dữ liệu.</summary>
internal sealed class DeleteSiteHandler(SiteContext context) : ICommandHandler<DeleteSiteCommand>
{
    public async Task HandleAsync(DeleteSiteCommand command, CancellationToken cancellationToken)
    {
        var id = command.Id;
        var rowVersion = command.RowVersion;
        context.Context.EnsureAuthorized();

        var entity = await context.store.FindSiteAsync(id, cancellationToken) ?? throw Missing("Site");

        if (await context.store.AnyAreasAsync(x => x.SiteId == id, cancellationToken)
            || await context.store.AnyShiftsAsync(x => x.SiteId == id, cancellationToken)
            || await context.store.AnyOeeTargetsAsync(x => x.SiteId == id, cancellationToken))
            throw Referenced("Site");
        context.Context.SoftDelete(entity, rowVersion);
        await context.Context.UnitOfWork.SaveChangesAsync(cancellationToken);
    }
}
