using Oee.Application.Common;
using Oee.Application.Downtime.Events.Common;

namespace Oee.Application.Downtime.Events.Close;

/// <summary>Điều phối thao tác CloseDowntimeEvent trong cùng scope lưu dữ liệu.</summary>
internal sealed class CloseDowntimeEventHandler(DowntimeContext context)
    : ICommandHandler<CloseDowntimeEventCommand>
{
    public async Task HandleAsync(
        CloseDowntimeEventCommand command,
        CancellationToken cancellationToken)
    {
        var id = command.Id;
        var endAt = command.EndAt;
        var concurrencyToken = command.ConcurrencyToken;
        context.EnsureAuthorized();

        var item = await context.FindAsync(id, cancellationToken);
        context.ApplyConcurrency(item, concurrencyToken);
        item.Close(endAt);
        item.RecordUpdate(context.Clock.UtcNow);
        await context.UnitOfWork.SaveChangesAsync(cancellationToken);
    }
}
