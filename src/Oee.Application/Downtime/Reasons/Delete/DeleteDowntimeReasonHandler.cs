using Oee.Application.Common;
using Oee.Application.Downtime.Reasons.Common;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Downtime.Reasons.Delete;

/// <summary>Điều phối thao tác DeleteDowntimeReason trong cùng scope lưu dữ liệu.</summary>
internal sealed class DeleteDowntimeReasonHandler(DowntimeReasonContext context)
    : ICommandHandler<DeleteDowntimeReasonCommand>
{
    public async Task HandleAsync(
        DeleteDowntimeReasonCommand command,
        CancellationToken cancellationToken)
    {
        var id = command.Id;
        var rowVersion = command.RowVersion;
        context.Context.EnsureAuthorized();

        var entity = await context.store.FindDowntimeReasonAsync(id, cancellationToken) ?? throw Missing("Downtime reason");

        if (entity.Code == "DR-UNKNOWN")
            throw new InvalidOperationException("DR-UNKNOWN is a protected system reason and cannot be deleted.");

        if (await context.store.AnyDowntimeEventsAsync(x => x.DowntimeReasonId == id, cancellationToken)
            || await context.store.AnyPlcFaultCodeMappingsAsync(x => x.DowntimeReasonId == id, cancellationToken))
            throw Referenced("Downtime reason");
        context.Context.SoftDelete(entity, rowVersion);
        await context.Context.UnitOfWork.SaveChangesAsync(cancellationToken);
    }
}
