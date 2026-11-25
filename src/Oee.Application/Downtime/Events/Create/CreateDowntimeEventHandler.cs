using Oee.Application.Common;
using Oee.Application.Downtime.Events.Common;
using Oee.Domain.Downtime;

namespace Oee.Application.Downtime.Events.Create;

/// <summary>Điều phối thao tác CreateDowntimeEvent trong cùng scope lưu dữ liệu.</summary>
internal sealed class CreateDowntimeEventHandler(DowntimeContext context)
    : IRequestHandler<CreateDowntimeEventCommand, Guid>
{
    public async Task<Guid> HandleAsync(
        CreateDowntimeEventCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        context.EnsureAuthorized();

        // Event đóng phải có end sau start; event mở chỉ có StartAt.
        if (request.EndAt.HasValue && request.EndAt <= request.StartAt)
            throw new ArgumentException("Invalid downtime window.");

        if (!request.EndAt.HasValue && await context.Downtimes.HasOpenAsync(request.WorkUnitId, cancellationToken))
            throw new InvalidOperationException("The work unit already has an open downtime.");
        await context.EnsureNoOverlapAsync(request.WorkUnitId, request.StartAt, request.EndAt, cancellationToken);
        // Chụp Category/Kind từ master vào event để metric độ tin cậy dùng đúng phân loại tại thời điểm ghi.
        var reason = await context.Downtimes.FindReasonAsync(request.DowntimeReasonId, cancellationToken)
            ?? throw new KeyNotFoundException("Downtime reason was not found.");

        var item = DowntimeEventEntity.Create(
            workUnitId: request.WorkUnitId,
            downtimeReasonId: reason.Id,
            category: reason.Category,
            kind: reason.Kind,
            startAt: request.StartAt,
            source: Domain.Common.DataSourceEnum.Manual,
            endAt: request.EndAt,
            productionOutputId: request.ProductionOutputId,
            note: request.Note);

        context.Downtimes.Add(item);
        await context.UnitOfWork.SaveChangesAsync(cancellationToken);

        return item.Id;
    }
}
