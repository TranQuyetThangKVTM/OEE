using Oee.Application.Common;
using Oee.Application.Organizations.Areas.Common;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Organizations.Areas.Delete;

/// <summary>Điều phối thao tác DeleteArea trong cùng scope lưu dữ liệu.</summary>
internal sealed class DeleteAreaHandler(AreaContext context) : ICommandHandler<DeleteAreaCommand>
{
    public async Task HandleAsync(DeleteAreaCommand command, CancellationToken cancellationToken)
    {
        var id = command.Id;
        var rowVersion = command.RowVersion;
        context.Context.EnsureAuthorized();

        var entity = await context.store.FindAreaAsync(id, cancellationToken) ?? throw Missing("Area");

        if (await context.store.AnyWorkCentersAsync(x => x.AreaId == id, cancellationToken))
            throw Referenced("Area");
        context.Context.SoftDelete(entity, rowVersion);
        await context.Context.UnitOfWork.SaveChangesAsync(cancellationToken);
    }
}
