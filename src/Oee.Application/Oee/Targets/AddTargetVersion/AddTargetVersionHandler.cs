using Oee.Application.Common;
using Oee.Application.Oee.Targets.Common;
using Oee.Domain.Entities.Oee;

namespace Oee.Application.Oee.Targets.AddTargetVersion;

/// <summary>Điều phối thao tác AddTargetVersion trong cùng scope lưu dữ liệu.</summary>
internal sealed class AddTargetVersionHandler(EffectiveConfigurationContext context)
    : IRequestHandler<AddTargetVersionCommand, Guid>
{
    public async Task<Guid> HandleAsync(
        AddTargetVersionCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request; // Một target không thể đồng thời thuộc cả WorkUnit và Site; tỷ lệ database luôn dùng 0..1.

        if ((request.WorkUnitId.HasValue && request.SiteId.HasValue) || request.EffectiveTo < request.EffectiveFrom)
            throw new ArgumentException("Target scope and effective interval are invalid.");
        EffectiveConfigurationValidator.ValidateRatio(request.MinimumOee);
        EffectiveConfigurationValidator.ValidateRatio(request.MinimumAvailability);
        EffectiveConfigurationValidator.ValidateRatio(request.MinimumPerformance);
        EffectiveConfigurationValidator.ValidateRatio(request.MinimumQuality);
        var periodType = (Domain.Common.OeePeriodTypeEnum)request.PeriodType;
        var periodCode = OeeTargetEntity.NormalizePeriodCode(periodType, request.PeriodCode);
        // Gửi tiêu chí qua port để adapter lọc phiên bản hiệu lực tại database.
        var previous = await context.Targets.FindPreviousAsync(
            x => x.WorkUnitId == request.WorkUnitId && x.SiteId == request.SiteId && x.PeriodType == periodType
                && x.PeriodCode == periodCode
                && x.EffectiveFrom < request.EffectiveFrom
                && (!x.EffectiveTo.HasValue || x.EffectiveTo >= request.EffectiveFrom),
            cancellationToken);

        if (previous is not null)
            previous.EndEffectiveness(request.EffectiveFrom.AddDays(-1));

        if (await context.Targets.AnyAsync(
            x => x.WorkUnitId == request.WorkUnitId && x.SiteId == request.SiteId && x.PeriodType == periodType
                && x.PeriodCode == periodCode
                && x.EffectiveFrom >= request.EffectiveFrom
                && (!request.EffectiveTo.HasValue || x.EffectiveFrom <= request.EffectiveTo),
            cancellationToken))
            throw new InvalidOperationException("The target interval overlaps a later version.");

        var item = OeeTargetEntity.Create(
            periodType: periodType,
            periodCode: periodCode,
            minimumOee: request.MinimumOee,
            minimumAvailability: request.MinimumAvailability,
            minimumPerformance: request.MinimumPerformance,
            minimumQuality: request.MinimumQuality,
            effectiveFrom: request.EffectiveFrom,
            workUnitId: request.WorkUnitId,
            siteId: request.SiteId,
            effectiveTo: request.EffectiveTo,
            note: request.Note);

        context.Targets.Add(item);
        await context.UnitOfWork.SaveChangesAsync(cancellationToken);

        return item.Id;
    }
}
