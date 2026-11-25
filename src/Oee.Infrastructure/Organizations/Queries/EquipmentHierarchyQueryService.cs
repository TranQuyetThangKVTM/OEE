using Microsoft.EntityFrameworkCore;
using Oee.Application.Contracts.Modules;
using Oee.Application.Modules;
using Oee.Domain.Common;
using Oee.Infrastructure.Persistence;

namespace Oee.Infrastructure.Operations;

/// <summary>Projection cây trên schema hiện tại; chỉ query con khi UI mở nhánh.</summary>
public sealed class EquipmentHierarchyQueryService(DbContextOptions<OeeDbContext> options) : IEquipmentHierarchyQueryService
{
    public async Task<PagedResult<EquipmentHierarchyNode>> GetAsync(EquipmentHierarchyQuery request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ParentKind.HasValue != request.ParentId.HasValue
            || request.ParentKind is EquipmentHierarchyKind.WorkUnit
            || (request.ParentKind.HasValue && !Enum.IsDefined(request.ParentKind.Value)))
            throw new ArgumentException("Phạm vi cha của cây không hợp lệ.", nameof(request));

        await using var db = new OeeDbContext(options);
        var enterprises = db.Enterprises.AsNoTracking();
        var sites = db.Sites.AsNoTracking();
        var areas = db.Areas.AsNoTracking();
        var centers = db.WorkCenters.AsNoTracking();
        var units = db.WorkUnits.AsNoTracking();
        var enterpriseTerm = NormalizeSearch(request.EnterpriseSearch);
        var siteTerm = NormalizeSearch(request.SiteSearch);
        var areaTerm = NormalizeSearch(request.AreaSearch);
        var centerTerm = NormalizeSearch(request.WorkCenterSearch);
        var unitTerm = NormalizeSearch(request.WorkUnitSearch);
        if (enterpriseTerm is not null || siteTerm is not null || areaTerm is not null || centerTerm is not null || unitTerm is not null)
        {
            // Các ô kết hợp AND trên cùng đường dẫn, không ghép kết quả từ hai nhánh khác nhau.
            // Giữ riêng từng IQueryable để biểu thức cha/con không tham chiếu vòng khi lọc ngược lên.
            var matchingEnterprises = enterprises.Where(x => enterpriseTerm == null || x.Code.Contains(enterpriseTerm) || x.Name.Contains(enterpriseTerm));
            var matchingSites = sites.Where(x => (siteTerm == null || x.Code.Contains(siteTerm) || x.Name.Contains(siteTerm))
                && matchingEnterprises.Any(parent => parent.Id == x.EnterpriseId));
            var matchingAreas = areas.Where(x => (areaTerm == null || x.Code.Contains(areaTerm) || x.Name.Contains(areaTerm))
                && matchingSites.Any(parent => parent.Id == x.SiteId));
            var matchingCenters = centers.Where(x => (centerTerm == null || x.Code.Contains(centerTerm) || x.Name.Contains(centerTerm))
                && matchingAreas.Any(parent => parent.Id == x.AreaId));
            var matchingUnits = units.Where(x => (unitTerm == null || x.Code.Contains(unitTerm) || x.Name.Contains(unitTerm))
                && matchingCenters.Any(parent => parent.Id == x.WorkCenterId));

            // Chỉ yêu cầu có con khớp khi một ô ở cấp thấp hơn đang được dùng.
            // Vì vậy tìm Enterprise/Site vẫn giữ được các nhánh rỗng của cấp đó.
            var visibleCenters = unitTerm is null ? matchingCenters
                : matchingCenters.Where(x => matchingUnits.Any(child => child.WorkCenterId == x.Id));
            var visibleAreas = centerTerm is null && unitTerm is null ? matchingAreas
                : matchingAreas.Where(x => visibleCenters.Any(child => child.AreaId == x.Id));
            var visibleSites = areaTerm is null && centerTerm is null && unitTerm is null ? matchingSites
                : matchingSites.Where(x => visibleAreas.Any(child => child.SiteId == x.Id));
            var visibleEnterprises = siteTerm is null && areaTerm is null && centerTerm is null && unitTerm is null ? matchingEnterprises
                : matchingEnterprises.Where(x => visibleSites.Any(child => child.EnterpriseId == x.Id));
            enterprises = visibleEnterprises;
            sites = visibleSites;
            areas = visibleAreas;
            centers = visibleCenters;
            units = matchingUnits;
        }
        var term = request.Search?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
        {
            // Match ở cấp thấp vẫn giữ đường dẫn cha; match ở cha giữ các con của nhánh đó.
            units = units.Where(x => x.Code.Contains(term) || x.Name.Contains(term)
                || x.WorkCenter.Code.Contains(term) || x.WorkCenter.Name.Contains(term)
                || x.WorkCenter.Area.Code.Contains(term) || x.WorkCenter.Area.Name.Contains(term)
                || x.WorkCenter.Area.Site.Code.Contains(term) || x.WorkCenter.Area.Site.Name.Contains(term)
                || x.WorkCenter.Area.Site.Enterprise.Code.Contains(term) || x.WorkCenter.Area.Site.Enterprise.Name.Contains(term));
            centers = centers.Where(x => x.Code.Contains(term) || x.Name.Contains(term)
                || x.Area.Code.Contains(term) || x.Area.Name.Contains(term)
                || x.Area.Site.Code.Contains(term) || x.Area.Site.Name.Contains(term)
                || x.Area.Site.Enterprise.Code.Contains(term) || x.Area.Site.Enterprise.Name.Contains(term)
                || units.Any(unit => unit.WorkCenterId == x.Id));
            areas = areas.Where(x => x.Code.Contains(term) || x.Name.Contains(term)
                || x.Site.Code.Contains(term) || x.Site.Name.Contains(term)
                || x.Site.Enterprise.Code.Contains(term) || x.Site.Enterprise.Name.Contains(term)
                || centers.Any(center => center.AreaId == x.Id));
            sites = sites.Where(x => x.Code.Contains(term) || x.Name.Contains(term)
                || x.Enterprise.Code.Contains(term) || x.Enterprise.Name.Contains(term)
                || areas.Any(area => area.SiteId == x.Id));
            enterprises = enterprises.Where(x => x.Code.Contains(term) || x.Name.Contains(term)
                || sites.Any(site => site.EnterpriseId == x.Id));
        }

        // Member-init projection giữ các cột sortable ở SQL; encode token sau khi đã Skip/Take.
        IQueryable<EquipmentHierarchyNodeProjection> nodes = request.ParentKind switch
        {
            null => enterprises.Select(x => new EquipmentHierarchyNodeProjection
            {
                Id = x.Id, Kind = EquipmentHierarchyKind.Enterprise, Code = x.Code, Name = x.Name,
                ChildCount = db.Sites.Count(child => child.EnterpriseId == x.Id),
                Status = x.Status, Description = x.Description, UpdatedAt = x.UpdatedAt ?? x.CreatedAt, RowVersion = x.RowVersion,
            }),
            EquipmentHierarchyKind.Enterprise => sites.Where(x => x.EnterpriseId == request.ParentId)
                .Select(x => new EquipmentHierarchyNodeProjection
                {
                    Id = x.Id, ParentId = x.EnterpriseId, Kind = EquipmentHierarchyKind.Site, Code = x.Code, Name = x.Name,
                    ChildCount = db.Areas.Count(child => child.SiteId == x.Id),
                    Status = x.Status, Description = x.Description, UpdatedAt = x.UpdatedAt ?? x.CreatedAt, RowVersion = x.RowVersion,
                }),
            EquipmentHierarchyKind.Site => areas.Where(x => x.SiteId == request.ParentId)
                .Select(x => new EquipmentHierarchyNodeProjection
                {
                    Id = x.Id, ParentId = x.SiteId, Kind = EquipmentHierarchyKind.Area, Code = x.Code, Name = x.Name,
                    ChildCount = db.WorkCenters.Count(child => child.AreaId == x.Id),
                    Status = x.Status, Description = x.Description, UpdatedAt = x.UpdatedAt ?? x.CreatedAt, RowVersion = x.RowVersion,
                }),
            EquipmentHierarchyKind.Area => centers.Where(x => x.AreaId == request.ParentId)
                .Select(x => new EquipmentHierarchyNodeProjection
                {
                    Id = x.Id, ParentId = x.AreaId, Kind = EquipmentHierarchyKind.WorkCenter, Code = x.Code, Name = x.Name,
                    ChildCount = db.WorkUnits.Count(child => child.WorkCenterId == x.Id),
                    Status = x.Status, Description = x.Description, UpdatedAt = x.UpdatedAt ?? x.CreatedAt, RowVersion = x.RowVersion,
                }),
            EquipmentHierarchyKind.WorkCenter => units.Where(x => x.WorkCenterId == request.ParentId)
                .Select(x => new EquipmentHierarchyNodeProjection
                {
                    Id = x.Id, ParentId = x.WorkCenterId, Kind = EquipmentHierarchyKind.WorkUnit, Code = x.Code, Name = x.Name,
                    Status = x.Status, Description = x.Description, UpdatedAt = x.UpdatedAt ?? x.CreatedAt, RowVersion = x.RowVersion,
                    OperationalState = db.PlcDeviceRuntimeStates.Where(state => state.PlcDeviceMapping.WorkUnitId == x.Id)
                        .Select(state => (OperationalStateEnum?)state.OperationalState).FirstOrDefault() ?? x.OperationalState,
                    ConnectivityState = db.PlcDeviceRuntimeStates.Where(state => state.PlcDeviceMapping.WorkUnitId == x.Id)
                        .Select(state => (ConnectivityStateEnum?)state.ConnectivityState).FirstOrDefault() ?? ConnectivityStateEnum.NotConfigured,
                }),
            _ => throw new ArgumentException("Cấp cha không hợp lệ.", nameof(request)),
        };
        var page = Math.Clamp(request.Page, 1, 1_000_000);
        var size = Math.Clamp(request.PageSize, 1, 200);
        var total = await nodes.CountAsync(ct);
        var items = await nodes.OrderBy(x => x.Code).ThenBy(x => x.Id)
            .Skip((page - 1) * size).Take(size).ToArrayAsync(ct);
        return new(items.Select(x => new EquipmentHierarchyNode(x.Id, x.ParentId, x.Kind, x.Code, x.Name,
            x.ChildCount, x.Status.ToString(), x.UpdatedAt, Convert.ToBase64String(x.RowVersion),
            x.OperationalState?.ToString(), x.ConnectivityState?.ToString(), x.Description)).ToArray(), total);
    }

    private static string? NormalizeSearch(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
