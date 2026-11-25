using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Operations;
using Oee.Application.Organizations.Shifts.Common;
using Oee.Domain.Common;
using Oee.Domain.Entities.Organization;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Organizations.Shifts.Create;

/// <summary>Điều phối thao tác CreateShift trong cùng scope lưu dữ liệu.</summary>
internal sealed class CreateShiftHandler(ShiftContext context)
    : IRequestHandler<CreateShiftCommand, MasterMutationResult>
{
    public async Task<MasterMutationResult> HandleAsync(
        CreateShiftCommand command,
        CancellationToken cancellationToken)
    {
        context.Context.EnsureAuthorized();

        var values = CreateShiftValidator.ValidateShift(command);
        await MasterDataCommandContext.EnsureActiveAsync(
            () => context.store.AnySitesAsync(x => x.Id == values.SiteId && x.Status == RecordStatusEnum.Active, cancellationToken),
            values.SiteId,
            "Site");
        await context.EnsureUniqueShiftCodeAsync(values.SiteId, values.Code, null, cancellationToken);
        await context.EnsureShiftDoesNotOverlapAsync(values.SiteId, Guid.Empty, values.Start, values.End, cancellationToken);

        var entity = ShiftEntity.Create(
            siteId: values.SiteId,
            code: values.Code,
            name: values.Name,
            startTime: values.Start,
            endTime: values.End,
            status: values.Status);

        context.store.AddShift(entity);
        await context.Context.SaveCreateAsync("Shift", cancellationToken);

        return Result(entity);
    }
}
