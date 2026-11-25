using Oee.Application.Common;
using Oee.Application.Oee.Alerts.Common;
using Oee.Application.Oee.Snapshots.Recalculate;
using Oee.Domain.Common;

namespace Oee.Application.Oee.Alerts.Reevaluate;

/// <summary>Điều phối thao tác ReevaluateOeeAlert trong cùng scope lưu dữ liệu.</summary>
internal sealed class ReevaluateOeeAlertHandler(AlertModuleContext context)
    : ICommandHandler<ReevaluateOeeAlertCommand>
{
    public async Task HandleAsync(
        ReevaluateOeeAlertCommand command,
        CancellationToken cancellationToken)
    {
        var id = command.Id;
        var concurrencyToken = command.ConcurrencyToken;
        var source = await context.Alerts.FindWithSnapshotAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException("Alert not found.");
        AlertModuleContext.EnsureConcurrency(source, concurrencyToken);

        if (source.State == AlertStateEnum.Resolved)
            throw new InvalidOperationException("A resolved alert cannot be re-evaluated.");
        await context.Requests.ExecuteAsync(
            new RecalculateOeeSnapshotCommand(
                source.WorkUnitId,
                source.Snapshot.PeriodType,
                source.Snapshot.PeriodCode,
                source.Snapshot.PeriodStart,
                source.Snapshot.PeriodEnd),
            cancellationToken);
        var alert = await context.FindAlertAsync(id, cancellationToken);
        context.Touch(alert);
        await context.UnitOfWork.SaveChangesAsync(cancellationToken);
    }
}
