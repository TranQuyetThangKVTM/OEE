using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Oee.Application.Common;
using Oee.Application.Contracts.Modules;
using Oee.Application.Oee;
using Oee.Domain.Common;
using Oee.Infrastructure.Persistence;

namespace Oee.Infrastructure.Operations;

public sealed class EfAlertReadStore(EfApplicationUnitOfWork unitOfWork, IClock clock) : IAlertReadStore
{
    private OeeDbContext db => unitOfWork.Db;
    /// <summary>Đọc một trang alert theo search/state/severity/date.</summary>
    public async Task<PagedResult<AlertListItem>> ListAsync(AlertListQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var source = db.OeeAlerts.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            source = source.Where(alert => alert.WorkUnit.Code.Contains(search) || alert.WorkUnit.Name.Contains(search));
        }
        if (!string.IsNullOrWhiteSpace(query.State))
        {
            if (!Enum.TryParse<AlertStateEnum>(query.State, true, out var state)) throw new ArgumentException("Invalid alert state.", nameof(query));
            source = source.Where(alert => alert.State == state);
        }
        if (!string.IsNullOrWhiteSpace(query.Severity))
        {
            if (!Enum.TryParse<AlertSeverityEnum>(query.Severity, true, out var severity)) throw new ArgumentException("Invalid alert severity.", nameof(query));
            source = source.Where(alert => alert.Severity == severity);
        }
        if (query.FromDate.HasValue)
        {
            var from = new DateTimeOffset(query.FromDate.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            source = source.Where(alert => alert.TriggeredAt >= from);
        }
        if (query.ToDate.HasValue)
        {
            var until = new DateTimeOffset(query.ToDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            source = source.Where(alert => alert.TriggeredAt < until);
        }

        // Count và page dùng cùng IQueryable/filter; projection chỉ lấy cột DTO cần hiển thị.
        var total = await source.CountAsync(ct);
        var ordered = (query.SortBy?.ToLowerInvariant(), query.Descending) switch
        {
            ("workunit", false) => source.OrderBy(alert => alert.WorkUnit.Code).ThenByDescending(alert => alert.TriggeredAt),
            ("workunit", true) => source.OrderByDescending(alert => alert.WorkUnit.Code).ThenByDescending(alert => alert.TriggeredAt),
            ("metric", false) => source.OrderBy(alert => alert.Metric).ThenByDescending(alert => alert.TriggeredAt),
            ("metric", true) => source.OrderByDescending(alert => alert.Metric).ThenByDescending(alert => alert.TriggeredAt),
            ("severity", false) => source.OrderBy(alert => alert.Severity).ThenByDescending(alert => alert.TriggeredAt),
            ("severity", true) => source.OrderByDescending(alert => alert.Severity).ThenByDescending(alert => alert.TriggeredAt),
            ("state", false) => source.OrderBy(alert => alert.State).ThenByDescending(alert => alert.TriggeredAt),
            ("state", true) => source.OrderByDescending(alert => alert.State).ThenByDescending(alert => alert.TriggeredAt),
            ("triggeredat", false) => source.OrderBy(alert => alert.TriggeredAt),
            _ => source.OrderByDescending(alert => alert.TriggeredAt),
        };
        var rows = await ordered.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(alert => new
            {
                alert.Id,
                alert.SnapshotId,
                Code = alert.WorkUnit.Code,
                Name = alert.WorkUnit.Name,
                alert.Snapshot.PeriodCode,
                PeriodType = alert.Snapshot.PeriodType.ToString(),
                Metric = alert.Metric.ToString(),
                Severity = alert.Severity.ToString(),
                State = alert.State.ToString(),
                alert.ActualValue,
                alert.TargetValue,
                alert.TriggeredAt,
                alert.AcknowledgedAt,
                alert.AcknowledgedBy,
                alert.ResolvedAt,
                alert.Note,
                alert.CreatedAt,
                alert.UpdatedAt,
            }).ToArrayAsync(ct);

        return new(rows.Select(row => new AlertListItem(
            row.Id, row.SnapshotId, row.Code, row.Name, row.PeriodCode, row.PeriodType,
            row.Metric, row.Severity, row.State, row.ActualValue, row.TargetValue,
            row.ActualValue - row.TargetValue, row.TriggeredAt, row.AcknowledgedAt,
            row.AcknowledgedBy, row.ResolvedAt, row.Note,
            (row.UpdatedAt ?? row.CreatedAt).ToString("O", CultureInfo.InvariantCulture))).ToArray(), total);
    }

    /// <summary>Đọc target versions và tính trạng thái hiệu lực tại ngày được chọn.</summary>
    public async Task<PagedResult<TargetListItem>> TargetsAsync(TargetListQuery query, CancellationToken ct)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var effectiveOn = query.EffectiveOn ?? DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var source = db.OeeTargets.AsNoTracking().AsQueryable();
        if (query.EffectiveOnly) source = source.Where(target => target.EffectiveFrom <= effectiveOn && (!target.EffectiveTo.HasValue || target.EffectiveTo >= effectiveOn));
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            source = source.Where(target => (target.WorkUnit != null && (target.WorkUnit.Code.Contains(search) || target.WorkUnit.Name.Contains(search)))
                || (target.Site != null && (target.Site.Code.Contains(search) || target.Site.Name.Contains(search)))
                || target.PeriodCode.Contains(search) || (target.Note != null && target.Note.Contains(search)));
        }
        var total = await source.CountAsync(ct);
        var ordered = (query.SortBy?.ToLowerInvariant(), query.Descending) switch
        {
            ("scope", false) => source.OrderBy(target => target.WorkUnitId).ThenBy(target => target.SiteId),
            ("scope", true) => source.OrderByDescending(target => target.WorkUnitId).ThenByDescending(target => target.SiteId),
            ("period", false) => source.OrderBy(target => target.PeriodType).ThenBy(target => target.PeriodCode),
            ("period", true) => source.OrderByDescending(target => target.PeriodType).ThenByDescending(target => target.PeriodCode),
            ("from", false) => source.OrderBy(target => target.EffectiveFrom),
            _ => source.OrderByDescending(target => target.EffectiveFrom),
        };
        var rows = await ordered.Skip((page - 1) * pageSize).Take(pageSize).Select(target => new
        {
            target.Id,
            target.WorkUnitId,
            target.SiteId,
            WorkUnitCode = target.WorkUnit != null ? target.WorkUnit.Code : null,
            SiteCode = target.Site != null ? target.Site.Code : null,
            PeriodType = target.PeriodType.ToString(),
            target.PeriodCode,
            target.MinimumOee,
            target.MinimumAvailability,
            target.MinimumPerformance,
            target.MinimumQuality,
            target.EffectiveFrom,
            target.EffectiveTo,
            target.Note,
            target.RowVersion,
        }).ToArrayAsync(ct);
        return new(rows.Select(row => new TargetListItem(
            row.Id, row.WorkUnitId, row.SiteId, row.WorkUnitCode ?? row.SiteCode ?? "Global",
            row.PeriodType, row.PeriodCode, row.MinimumOee, row.MinimumAvailability,
            row.MinimumPerformance, row.MinimumQuality, row.EffectiveFrom, row.EffectiveTo,
            row.Note, row.EffectiveFrom <= effectiveOn && (!row.EffectiveTo.HasValue || row.EffectiveTo >= effectiveOn),
            Convert.ToBase64String(row.RowVersion))).ToArray(), total);
    }

}
