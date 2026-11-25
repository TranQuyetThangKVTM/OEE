using Microsoft.EntityFrameworkCore;
using Oee.Application.Contracts.Modules;
using Oee.Application.Modules;
using Oee.Infrastructure.Persistence;

namespace Oee.Infrastructure.Operations;

/// <summary>
/// Adapter truy vấn dùng chung cho lookup API.
/// </summary>
/// <remarks>
/// Các query dùng AsNoTracking vì chỉ đọc và Select trực tiếp sang DTO để EF dịch xuống SQL,
/// tránh tải graph Entity. CancellationToken được truyền tới thao tác database.
/// </remarks>
public sealed class ModuleReadServices(OeeDbContext db) : IMasterOrganizationQueryService
{
    /// <summary>Chọn projection theo loại master mà caller yêu cầu.</summary>
    public async Task<IReadOnlyCollection<MasterRow>> GetAsync(string kind, CancellationToken ct) => kind switch
    {
        "products" => await db.Products.AsNoTracking().Select(x => new MasterRow(x.Id, x.Code, x.Name, x.DefaultUom, x.Status.ToString())).ToArrayAsync(ct),
        "standards" => await db.WorkUnitStandards.AsNoTracking().Select(x => new MasterRow(x.Id, x.WorkUnit.Code + " / " + x.Product.Code, x.StandardSpeedPerMinute + " " + x.Uom + "/phút", x.EffectiveFrom + " → " + x.EffectiveTo, "Active")).ToArrayAsync(ct),
        "shifts" => await db.Shifts.AsNoTracking().Select(x => new MasterRow(x.Id, x.Code, x.Name, x.StartTime + "–" + x.EndTime, x.Status.ToString())).ToArrayAsync(ct),
        "reasons" => await db.DowntimeReasons.AsNoTracking().Select(x => new MasterRow(x.Id, x.Code, x.Name, x.Category + " / " + x.Kind, x.Status.ToString())).ToArrayAsync(ct),
        "workunits" => await db.WorkUnits.AsNoTracking().Select(x => new MasterRow(x.Id, x.Code, x.Name, x.WorkCenter.Name, x.Status.ToString())).ToArrayAsync(ct),
        "workcenters" or "groups" => await db.WorkCenters.AsNoTracking().Select(x => new MasterRow(x.Id, x.Code, x.Name, x.Area.Name, x.Status.ToString())).ToArrayAsync(ct),
        "areas" or "processes" => await db.Areas.AsNoTracking().Select(x => new MasterRow(x.Id, x.Code, x.Name, x.Site.Name, x.Status.ToString())).ToArrayAsync(ct),
        "enterprises" => await db.Enterprises.AsNoTracking().Select(x => new MasterRow(x.Id, x.Code, x.Name, x.Description ?? string.Empty, x.Status.ToString())).ToArrayAsync(ct),
        "sites" => await db.Sites.AsNoTracking().Select(x => new MasterRow(x.Id, x.Code, x.Name, x.TimeZoneId, x.Status.ToString())).ToArrayAsync(ct),
        _ => [],
    };
}
