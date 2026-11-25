using Oee.Application.Common;
using Oee.Application.Organizations.WorkCenters.Common;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Organizations.WorkCenters.Delete;

/// <summary>Điều phối thao tác DeleteWorkCenter trong cùng scope lưu dữ liệu.</summary>
internal sealed class DeleteWorkCenterHandler(WorkCenterContext context)
    : ICommandHandler<DeleteWorkCenterCommand>
{
    public async Task HandleAsync(DeleteWorkCenterCommand command, CancellationToken cancellationToken)
    {
        var id = command.Id;
        var rowVersion = command.RowVersion;
        context.Context.EnsureAuthorized();

        var entity = await context.store.FindWorkCenterAsync(id, cancellationToken) ?? throw Missing("Work center");

        if (await context.store.AnyWorkUnitsAsync(x => x.WorkCenterId == id, cancellationToken))
            throw Referenced("Work center");
        context.Context.SoftDelete(entity, rowVersion);
        await context.Context.UnitOfWork.SaveChangesAsync(cancellationToken);
    }
}
