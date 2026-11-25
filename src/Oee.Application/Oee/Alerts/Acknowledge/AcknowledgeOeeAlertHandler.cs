using Oee.Application.Common;
using Oee.Application.Oee.Alerts.Common;

namespace Oee.Application.Oee.Alerts.Acknowledge;

/// <summary>Điều phối thao tác AcknowledgeOeeAlert trong cùng scope lưu dữ liệu.</summary>
internal sealed class AcknowledgeOeeAlertHandler(AlertModuleContext context)
    : ICommandHandler<AcknowledgeOeeAlertCommand>
{
    public async Task HandleAsync(
        AcknowledgeOeeAlertCommand command,
        CancellationToken cancellationToken)
    {
        var id = command.Id;
        var request = command.Request;
        var alert = await context.FindAlertAsync(id, cancellationToken);
        AlertModuleContext.EnsureConcurrency(alert, request.ConcurrencyToken);
        alert.Acknowledge(context.Clock.UtcNow, context.CurrentActor.UserName, request.Note);
        context.Touch(alert);
        await context.UnitOfWork.SaveChangesAsync(cancellationToken);
    }
}
