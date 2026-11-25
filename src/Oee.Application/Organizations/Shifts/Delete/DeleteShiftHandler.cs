using Oee.Application.Common;
using Oee.Application.Organizations.Shifts.Common;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Organizations.Shifts.Delete;

/// <summary>Điều phối thao tác DeleteShift trong cùng scope lưu dữ liệu.</summary>
internal sealed class DeleteShiftHandler(ShiftContext context) : ICommandHandler<DeleteShiftCommand>
{
    public async Task HandleAsync(DeleteShiftCommand command, CancellationToken cancellationToken)
    {
        var id = command.Id;
        var rowVersion = command.RowVersion;
        context.Context.EnsureAuthorized();

        var entity = await context.store.FindShiftAsync(id, cancellationToken) ?? throw Missing("Shift");

        if (await context.store.AnyProductionPlansAsync(x => x.ShiftId == id, cancellationToken)
            || await context.store.AnyProductionOutputsAsync(x => x.ShiftId == id, cancellationToken)
            || await context.store.AnySampleProductionPlanItemsAsync(x => x.ShiftId == id, cancellationToken))
            throw Referenced("Shift");
        context.Context.SoftDelete(entity, rowVersion);
        await context.Context.UnitOfWork.SaveChangesAsync(cancellationToken);
    }
}
