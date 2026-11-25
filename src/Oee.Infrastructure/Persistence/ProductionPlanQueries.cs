using Microsoft.EntityFrameworkCore;
using Oee.Domain.Entities.Production;

namespace Oee.Infrastructure.Persistence;

/// <summary>Chọn kế hoạch sử dụng, giữ các bản gốc cho lịch sử sản lượng.</summary>
internal static class ProductionPlanQueries
{
    public static IQueryable<ProductionPlanEntity> CurrentProductionPlans(this OeeDbContext db) =>
        db.ProductionPlans.Where(plan => !plan.DeletedAt.HasValue
            // Chỉ tương thích dữ liệu cũ: 3 = đã xóa, 4 = đã thay thế. Không có vòng đời phát hành mới.
            && EF.Property<int>(plan, "Status") != 3 && EF.Property<int>(plan, "Status") != 4
            && !db.ProductionPlans.IgnoreQueryFilters().Any(next => next.ReplacesPlanId == plan.Id));
}
