using Microsoft.EntityFrameworkCore;
using Oee.Application.Audit;
using Oee.Application.Contracts.Audit;
using Oee.Application.Security;
using Oee.Infrastructure.Persistence;

namespace Oee.Infrastructure.Audit;

/// <summary>Query service đọc AuditLog append-only cho trang quản trị.</summary>
/// <remarks>
/// Search/filter/sort/paging chạy tại SQL bằng AsNoTracking. AuditRead bảo vệ cả list/detail;
/// AuditRawJsonRead quyết định DTO detail có trả JSON kỹ thuật đã sanitize hay không.
/// </remarks>
public sealed class AuditQueryService(
    OeeDbContext dbContext,
    ICapabilityChecker? capabilityChecker = null) : IAuditQueryService
{
    /// <summary>Tìm audit theo user/entity/action/time và trả một trang mới nhất trước mặc định.</summary>
    public async Task<PagedAuditLogResponse> SearchAsync(
        AuditLogSearchRequest request,
        CancellationToken cancellationToken)
    {
        EnsureReadAuthorized();
        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var query = dbContext.AuditLogs.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.User))
        {
            var user = request.User.Trim();
            query = query.Where(log =>
                (log.UserName != null && log.UserName.Contains(user))
                || (log.UserId != null && log.UserId.Contains(user)));
        }

        if (!string.IsNullOrWhiteSpace(request.EntityType))
        {
            var entityType = request.EntityType.Trim();
            // Tên mới vẫn đọc được audit cũ mà không sửa nội dung log append-only.
            if (entityType.Equals("Area", StringComparison.OrdinalIgnoreCase)
                || entityType.Equals("Process", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(log => log.EntityType == "Area" || log.EntityType == "Process");
            }
            else if (entityType.Equals("WorkCenter", StringComparison.OrdinalIgnoreCase)
                || entityType.Equals("WorkUnitGroup", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(log => log.EntityType == "WorkCenter" || log.EntityType == "WorkUnitGroup");
            }
            else
            {
                query = query.Where(log => log.EntityType == entityType);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            var action = request.Action.Trim();
            query = query.Where(log => log.Action == action);
        }

        if (request.From.HasValue)
        {
            query = query.Where(log => log.Timestamp >= request.From.Value);
        }

        if (request.Until.HasValue)
        {
            query = query.Where(log => log.Timestamp < request.Until.Value);
        }

        // Count dùng cùng filter trước Skip/Take để tổng số phản ánh đúng điều kiện UI.
        var total = await query.CountAsync(cancellationToken);
        query = (request.SortBy, request.Descending) switch
        {
            ("user", true) => query.OrderByDescending(log => log.UserName),
            ("user", false) => query.OrderBy(log => log.UserName),
            ("entity", true) => query.OrderByDescending(log => log.EntityType),
            ("entity", false) => query.OrderBy(log => log.EntityType),
            ("action", true) => query.OrderByDescending(log => log.Action),
            ("action", false) => query.OrderBy(log => log.Action),
            ("subject", true) => query.OrderByDescending(log => log.EntitySubject),
            ("subject", false) => query.OrderBy(log => log.EntitySubject),
            (_, false) => query.OrderBy(log => log.Timestamp),
            _ => query.OrderByDescending(log => log.Timestamp),
        };

        var items = await query
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(log => new AuditLogResponse(log.Id, log.Timestamp, log.UserId, log.UserName, log.Action,
                log.EntityType, log.EntityId, log.EntitySubject, log.IpAddress))
            .ToArrayAsync(cancellationToken);
        return new PagedAuditLogResponse(items, page, pageSize, total);
    }

    /// <summary>Đọc snapshot audit theo Id và map nội dung thân thiện/raw theo quyền.</summary>
    public async Task<AuditLogDetailResponse> DetailAsync(Guid auditLogId, CancellationToken cancellationToken)
    {
        EnsureReadAuthorized();
        var log = await dbContext.AuditLogs.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == auditLogId, cancellationToken)
            ?? throw new KeyNotFoundException($"Audit log '{auditLogId}' was not found.");
        var includeRawJson = capabilityChecker?.HasCapability(Capabilities.AuditRawJsonRead) ?? true;
        return AuditPresentationMapper.Map(log, includeRawJson);
    }

    private void EnsureReadAuthorized()
    {
        if (capabilityChecker is not null && !capabilityChecker.HasCapability(Capabilities.AuditRead))
        {
            throw new UnauthorizedAccessException("The audit.read capability is required.");
        }
    }
}
