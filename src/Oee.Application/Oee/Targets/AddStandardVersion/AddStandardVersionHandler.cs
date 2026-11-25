using Oee.Application.Common;
using Oee.Application.Oee.Targets.Common;
using Oee.Domain.Entities.Organization;

namespace Oee.Application.Oee.Targets.AddStandardVersion;

/// <summary>Điều phối thao tác AddStandardVersion trong cùng scope lưu dữ liệu.</summary>
internal sealed class AddStandardVersionHandler(EffectiveConfigurationContext context)
    : IRequestHandler<AddStandardVersionCommand, Guid>
{
    public async Task<Guid> HandleAsync(
        AddStandardVersionCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request; // Kiểm tra giá trị và chiều của khoảng hiệu lực trước khi query SQL.

        if (request.SpeedPerMinute <= 0 || request.EffectiveTo < request.EffectiveFrom)
            throw new ArgumentException("Invalid standard version.");
        // Tìm phiên bản trước đang phủ ngày bắt đầu mới để kết thúc nó vào ngày liền trước.
        var previous = await context.Masters.FindPreviousStandardAsync(
            request.WorkUnitId,
            request.ProductId,
            request.EffectiveFrom,
            cancellationToken);

        if (previous is not null)
            previous.EndEffectiveness(request.EffectiveFrom.AddDays(-1));

        // Một phiên bản tương lai giao nhau sẽ bị từ chối, không ghi đè lịch sử.
        if (await context.Masters.AnyWorkUnitStandardsAsync(
            x => x.WorkUnitId == request.WorkUnitId && x.ProductId == request.ProductId
                && x.EffectiveFrom >= request.EffectiveFrom
                && (!request.EffectiveTo.HasValue || x.EffectiveFrom <= request.EffectiveTo),
            cancellationToken))
            throw new InvalidOperationException("The standard effective interval overlaps a later version.");

        var item = WorkUnitStandardEntity.Create(
            workUnitId: request.WorkUnitId,
            productId: request.ProductId,
            standardSpeedPerMinute: request.SpeedPerMinute,
            uom: request.Uom.Trim(),
            effectiveFrom: request.EffectiveFrom,
            effectiveTo: request.EffectiveTo);

        context.Masters.AddWorkUnitStandard(item);
        await context.UnitOfWork.SaveChangesAsync(cancellationToken);

        return item.Id;
    }
}
