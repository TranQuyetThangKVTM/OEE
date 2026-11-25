using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Downtime.Reasons.Common;
using Oee.Domain.Entities.Organization;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Downtime.Reasons.Create;

/// <summary>Điều phối thao tác CreateDowntimeReason trong cùng scope lưu dữ liệu.</summary>
internal sealed class CreateDowntimeReasonHandler(DowntimeReasonContext context)
    : IRequestHandler<CreateDowntimeReasonCommand, MasterMutationResult>
{
    public async Task<MasterMutationResult> HandleAsync(
        CreateDowntimeReasonCommand command,
        CancellationToken cancellationToken)
    {
        context.Context.EnsureAuthorized();

        var values = CreateDowntimeReasonValidator.ValidateReason(command);
        await context.EnsureUniqueReasonCodeAsync(values.Code, null, cancellationToken);

        var entity = DowntimeReasonEntity.Create(
            code: values.Code,
            name: values.Name,
            category: values.Category,
            kind: values.Kind,
            description: values.Description,
            status: values.Status);

        context.store.AddDowntimeReason(entity);
        await context.Context.SaveCreateAsync("Downtime reason", cancellationToken);

        return Result(entity);
    }
}
