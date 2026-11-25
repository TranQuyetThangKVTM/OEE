using Oee.Application.Common;
using Oee.Application.Downtime.Events.Common;

namespace Oee.Application.Downtime.Events.BulkReclassify;

/// <summary>Điều phối thao tác BulkReclassifyDowntimeEvent trong cùng scope lưu dữ liệu.</summary>
internal sealed class BulkReclassifyDowntimeEventHandler(DowntimeContext context)
    : IRequestHandler<BulkReclassifyDowntimeEventCommand, int>
{
    public async Task<int> HandleAsync(
        BulkReclassifyDowntimeEventCommand command,
        CancellationToken cancellationToken)
    {
        var requests = command.Requests;
        var reasonId = command.ReasonId;
        context.EnsureAuthorized();

        if (requests.Count == 0)
            return 0;
        var reason = await context.Downtimes.FindReasonAsync(reasonId, cancellationToken)
            ?? throw new KeyNotFoundException("Reason was not found.");
        var ids = requests.Select(x => x.DowntimeId).Distinct().ToArray();
        var items = await context.Downtimes.ReadAsync(ids, cancellationToken);

        if (items.Length != ids.Length)
            throw new KeyNotFoundException("One or more downtime events were not found.");

        // Mỗi dòng mang token riêng; một token cũ sẽ làm toàn bộ SaveChanges thất bại thay vì ghi dở dang.
        foreach (var item in items)
        {
            var request = requests.Single(x => x.DowntimeId == item.Id);
            context.ApplyConcurrency(item, request.ConcurrencyToken);
            item.Reclassify(reason);
            item.RecordUpdate(context.Clock.UtcNow);
        }

        await context.UnitOfWork.SaveChangesAsync(cancellationToken);

        return items.Length;
    }
}
