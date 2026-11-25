using Oee.Application.Common;
using Oee.Application.Downtime.Events.Common;

namespace Oee.Application.Downtime.Events.Reclassify;

/// <summary>Điều phối thao tác ReclassifyDowntimeEvent trong cùng scope lưu dữ liệu.</summary>
internal sealed class ReclassifyDowntimeEventHandler(DowntimeContext context)
    : ICommandHandler<ReclassifyDowntimeEventCommand>
{
    public async Task HandleAsync(
        ReclassifyDowntimeEventCommand command,
        CancellationToken cancellationToken)
    {
        var id = command.Id;
        var reasonId = command.ReasonId;
        var concurrencyToken = command.ConcurrencyToken;
        context.EnsureAuthorized();

        var item = await context.FindAsync(id, cancellationToken);
        context.ApplyConcurrency(item, concurrencyToken);
        var reason = await context.Downtimes.FindReasonAsync(reasonId, cancellationToken)
            ?? throw new KeyNotFoundException("Reason was not found.");
        item.Reclassify(reason);
        item.RecordUpdate(context.Clock.UtcNow);
        await context.UnitOfWork.SaveChangesAsync(cancellationToken);
    }
}
