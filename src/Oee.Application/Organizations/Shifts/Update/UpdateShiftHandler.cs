using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Organizations.Shifts.Common;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Organizations.Shifts.Update;

/// <summary>Điều phối thao tác UpdateShift trong cùng scope lưu dữ liệu.</summary>
internal sealed class UpdateShiftHandler(ShiftContext context)
    : IRequestHandler<UpdateShiftCommand, MasterMutationResult>
{
    public async Task<MasterMutationResult> HandleAsync(
        UpdateShiftCommand command,
        CancellationToken cancellationToken)
    {
        context.Context.EnsureAuthorized();

        if (command.StartTime == command.EndTime)
            throw new ArgumentException("Shift start/end must differ.");
        var entity = await context.store.FindShiftAsync(command.Id, cancellationToken) ?? throw Missing("Shift");
        context.Context.ApplyConcurrency(entity, command.RowVersion);
        await context.EnsureShiftDoesNotOverlapAsync(
            entity.SiteId,
            entity.Id,
            command.StartTime,
            command.EndTime,
            cancellationToken);
        entity.Update(command.Name, command.StartTime, command.EndTime);
        context.Context.Touch(entity);
        await context.Context.UnitOfWork.SaveChangesAsync(cancellationToken);

        return Result(entity);
    }
}
