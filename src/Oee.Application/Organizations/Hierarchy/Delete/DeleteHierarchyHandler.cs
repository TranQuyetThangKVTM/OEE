using System.Data;
using Oee.Application.Common;
using Oee.Application.Contracts.Modules;
using Oee.Application.Organizations.Areas.Delete;
using Oee.Application.Organizations.Enterprises.Delete;
using Oee.Application.Organizations.Hierarchy.Common;
using Oee.Application.Organizations.Sites.Delete;
using Oee.Application.Organizations.WorkCenters.Delete;
using Oee.Application.Organizations.WorkUnits.Delete;

namespace Oee.Application.Organizations.Hierarchy.Delete;

/// <summary>Điều phối thao tác DeleteHierarchy trong cùng scope lưu dữ liệu.</summary>
internal sealed class DeleteHierarchyHandler(HierarchyMutationContext context)
    : ICommandHandler<DeleteHierarchyCommand>
{
    public async Task HandleAsync(DeleteHierarchyCommand command, CancellationToken cancellationToken)
    {
        var items = command.Items;
        context.context.EnsureAuthorized();

        ArgumentNullException.ThrowIfNull(items);
        var selected = items.ToArray();

        if (selected.Length is < 1 or > 200
            || selected.Any(x => x is null || x.Id == Guid.Empty || !Enum.IsDefined(x.Kind) || string.IsNullOrWhiteSpace(x.RowVersion))
            || selected.Select(x => (x.Kind, x.Id)).Distinct().Count() != selected.Length)
            throw new ArgumentException("Chọn từ 1 đến 200 dòng hợp lệ, không trùng lặp.", nameof(command));
        await using var transaction = await context.UnitOfWork.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        try
        {
            // Chỉ xóa các Id đã chọn. Các command hiện có kiểm tra RowVersion, lịch sử và con chưa xóa.
            foreach (var item in selected.OrderByDescending(x => x.Kind).ThenBy(x => x.Id))
            {
                await (item.Kind switch
                {
                    EquipmentHierarchyKind.WorkUnit => context.Requests.ExecuteAsync(new DeleteWorkUnitCommand(item.Id, item.RowVersion), cancellationToken),
                    EquipmentHierarchyKind.WorkCenter => context.Requests.ExecuteAsync(new DeleteWorkCenterCommand(item.Id, item.RowVersion), cancellationToken),
                    EquipmentHierarchyKind.Area => context.Requests.ExecuteAsync(new DeleteAreaCommand(item.Id, item.RowVersion), cancellationToken),
                    EquipmentHierarchyKind.Site => context.Requests.ExecuteAsync(new DeleteSiteCommand(item.Id, item.RowVersion), cancellationToken),
                    EquipmentHierarchyKind.Enterprise => context.Requests.ExecuteAsync(new DeleteEnterpriseCommand(item.Id, item.RowVersion), cancellationToken),
                    _ => throw new ArgumentException("Cấp tổ chức không hợp lệ.", nameof(command)),
                });
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            context.UnitOfWork.DiscardChanges();
            throw;
        }
    }
}
