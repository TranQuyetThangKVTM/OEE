using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Downtime.Reasons.Common;
using Oee.Domain.Common;
using Oee.Domain.Entities.Organization;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Downtime.Reasons.Update;

/// <summary>Điều phối thao tác UpdateDowntimeReason trong cùng scope lưu dữ liệu.</summary>
internal sealed class UpdateDowntimeReasonHandler(DowntimeReasonContext context)
    : IRequestHandler<UpdateDowntimeReasonCommand, MasterMutationResult>
{
    public async Task<MasterMutationResult> HandleAsync(
        UpdateDowntimeReasonCommand command,
        CancellationToken cancellationToken)
    {
        context.Context.EnsureAuthorized();

        var entity = await context.store.FindDowntimeReasonAsync(command.Id, cancellationToken)
            ?? throw Missing("Downtime reason");

        if (!Enum.TryParse<DowntimeCategoryEnum>(command.Category, true, out var category)
            || !Enum.TryParse<DowntimeKindEnum>(command.Kind, true, out var kind))
            throw new ArgumentException("Downtime category or kind is invalid.");
        DowntimeReasonEntity.ValidateClassification(category, kind);

        if (entity.Code == "DR-UNKNOWN"
            && (category != DowntimeCategoryEnum.Unknown || kind != DowntimeKindEnum.Unknown))
            throw new InvalidOperationException("DR-UNKNOWN must keep Unknown category and kind.");
        context.Context.ApplyConcurrency(entity, command.RowVersion);
        entity.Update(command.Name, category, kind, command.Description);
        context.Context.Touch(entity);
        await context.Context.UnitOfWork.SaveChangesAsync(cancellationToken);

        return Result(entity);
    }
}
