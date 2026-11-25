using Microsoft.EntityFrameworkCore;
using Oee.Application.Contracts.Modules;
using Oee.Application.Contracts.PlcIntegration;
using Oee.Application.PlcIntegration;
using Oee.Domain.Common;
using Oee.Domain.PlcIntegration;
using Oee.Infrastructure.Persistence;

namespace Oee.Infrastructure.PlcIntegration;

public sealed class EfPlcConfigurationReadStore(EfApplicationUnitOfWork unitOfWork) : IPlcConfigurationReadStore
{
    private OeeDbContext db => unitOfWork.Db;
    /// <summary>Truy vấn danh sách kết nối OPC UA theo filter, sort và paging ở server.</summary>
    public async Task<PagedResult<OpcUaConnectionItem>> ConnectionsAsync(PlcListQuery query, CancellationToken ct)
    {
        var q = db.OpcUaConnections.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            q = q.Where(x => x.Code.Contains(search) || x.Name.Contains(search) || x.EndpointUrl.Contains(search));
        }
        if (!string.IsNullOrWhiteSpace(query.State) && bool.TryParse(query.State, out var active)) q = q.Where(x => x.IsActive == active);
        q = (query.SortBy?.ToLowerInvariant(), query.Descending) switch
        {
            ("name", false) => q.OrderBy(x => x.Name),
            ("name", true) => q.OrderByDescending(x => x.Name),
            ("endpoint", false) => q.OrderBy(x => x.EndpointUrl),
            ("endpoint", true) => q.OrderByDescending(x => x.EndpointUrl),
            (_, true) => q.OrderByDescending(x => x.Code),
            _ => q.OrderBy(x => x.Code),
        };
        var total = await q.CountAsync(ct);
        // Chỉ materialize đúng trang cần hiển thị; session được ghép theo batch sau khi biết các connection Id.
        var entities = await q.Skip(Offset(query.Page, query.PageSize)).Take(PageSize(query.PageSize)).ToArrayAsync(ct);
        var ids = entities.Select(x => x.Id).ToArray();
        var sessions = await db.PlcSessionEvents.AsNoTracking().Where(x => ids.Contains(x.OpcUaConnectionId))
            .OrderByDescending(x => x.OccurredAt).ToArrayAsync(ct);
        var items = entities.Select(x => MapConnection(x, sessions.FirstOrDefault(e => e.OpcUaConnectionId == x.Id))).ToArray();
        return new(items, total);
    }

    private static OpcUaConnectionItem MapConnection(OpcUaConnectionEntity x, PlcSessionEventEntity? session) => new(
        x.Id, x.Code, x.Name, x.EndpointUrl, x.SecurityMode, x.SecurityPolicy, x.AuthenticationType, x.UserName,
        x.CredentialSecretReference is not null, x.CertificateReference, x.IsActive, session?.State.ToString(),
        session?.OccurredAt, Convert.ToBase64String(x.RowVersion));

    /// <summary>Đếm và lấy một trang mapping bằng hai truy vấn; SQL ghép tag/checkpoint/runtime vào projection.</summary>
    public async Task<PagedResult<PlcDeviceMappingItem>> DeviceMappingsAsync(PlcListQuery query, CancellationToken ct)
    {
        var q = db.PlcDeviceMappings.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            q = q.Where(x => x.ExternalDeviceId.Contains(search) || x.WorkUnit.Code.Contains(search)
                || x.WorkUnit.Name.Contains(search) || x.OpcUaConnection.Code.Contains(search));
        }
        if (query.ScopeId.HasValue) q = q.Where(x => x.OpcUaConnectionId == query.ScopeId);
        if (!string.IsNullOrWhiteSpace(query.State) && bool.TryParse(query.State, out var active)) q = q.Where(x => x.IsActive == active);
        q = (query.SortBy?.ToLowerInvariant(), query.Descending) switch
        {
            ("workunit", false) => q.OrderBy(x => x.WorkUnit.Code),
            ("workunit", true) => q.OrderByDescending(x => x.WorkUnit.Code),
            ("lastread", false) => q.OrderBy(x => db.PlcDeviceRuntimeStates.Where(s => s.PlcDeviceMappingId == x.Id).Select(s => s.LastDataAt).FirstOrDefault()),
            ("lastread", true) => q.OrderByDescending(x => db.PlcDeviceRuntimeStates.Where(s => s.PlcDeviceMappingId == x.Id).Select(s => s.LastDataAt).FirstOrDefault()),
            (_, true) => q.OrderByDescending(x => x.ExternalDeviceId),
            _ => q.OrderBy(x => x.ExternalDeviceId),
        };
        var total = await q.CountAsync(ct);
        // Chỉ lấy cột cần hiển thị; không tải entity connection (chứa secret reference) về để ghép dòng.
        var rows = await q.Skip(Offset(query.Page, query.PageSize)).Take(PageSize(query.PageSize))
            .Select(x => new
            {
                x.Id, x.OpcUaConnectionId, Connection = x.OpcUaConnection.Code,
                x.WorkUnitId, WorkUnit = x.WorkUnit.Code, WorkUnitName = x.WorkUnit.Name, x.DefaultProductId,
                DefaultProduct = x.DefaultProduct != null ? x.DefaultProduct.Code : null,
                DefaultProductName = x.DefaultProduct != null ? x.DefaultProduct.Name : null,
                x.ExternalDeviceId, x.CounterMode, x.FaultCodeFormat,
                x.ZeroFaultCodeMeansNoFault, x.DebounceSamples, x.Note, x.IsActive, x.RowVersion,
                ReadTags = db.OpcUaTagMappings.Count(t => t.PlcDeviceMappingId == x.Id && t.Direction == TagDirectionEnum.Read),
                WriteTags = db.OpcUaTagMappings.Count(t => t.PlcDeviceMappingId == x.Id && t.Direction == TagDirectionEnum.Write),
                LastReadAt = db.CounterCheckpoints.Where(c => c.PlcDeviceMappingId == x.Id).Select(c => c.LastReadAt).FirstOrDefault(),
                LastReceivedAt = db.PlcDeviceRuntimeStates.Where(s => s.PlcDeviceMappingId == x.Id).Select(s => s.LastDataAt).FirstOrDefault(),
                CounterBlocked = db.CounterCheckpoints.Any(c => c.PlcDeviceMappingId == x.Id && (c.LastSampleKey == "!counter-discontinuity"
                    || c.LastSampleKey != null && c.LastSampleKey.StartsWith(CounterCheckpointEntity.RecoveryPendingPrefix))),
                LastTotal = db.CounterCheckpoints.Where(c => c.PlcDeviceMappingId == x.Id).Select(c => c.LastTotalCount).FirstOrDefault(),
                LastDefect = db.CounterCheckpoints.Where(c => c.PlcDeviceMappingId == x.Id).Select(c => c.LastDefectCount).FirstOrDefault(),
                Connectivity = db.PlcDeviceRuntimeStates.Where(s => s.PlcDeviceMappingId == x.Id)
                    .Select(s => (ConnectivityStateEnum?)s.ConnectivityState).FirstOrDefault() ?? ConnectivityStateEnum.NotConfigured
            }).ToArrayAsync(ct);
        var items = rows.Select(x => new PlcDeviceMappingItem(
            x.Id, x.OpcUaConnectionId, x.Connection, x.WorkUnitId, x.WorkUnit,
            x.DefaultProductId, x.DefaultProduct, x.ExternalDeviceId,
            x.CounterMode.ToString(), x.FaultCodeFormat.ToString(), x.ZeroFaultCodeMeansNoFault,
            x.DebounceSamples, x.Note, x.ReadTags, x.WriteTags, x.LastReadAt,
            x.Connectivity.ToString(), x.IsActive, Convert.ToBase64String(x.RowVersion),
            x.LastReceivedAt, x.CounterBlocked, x.LastTotal, x.LastDefect, x.WorkUnitName, x.DefaultProductName)).ToArray();
        return new(items, total);
    }

    /// <summary>Lấy đầy đủ cấu hình một PLC device để mở dialog xem/sửa.</summary>
    public async Task<PlcDeviceConfiguration> DeviceConfigurationAsync(Guid id, CancellationToken ct)
    {
        var entity = await db.PlcDeviceMappings.AsNoTracking().Include(x => x.OpcUaConnection).Include(x => x.WorkUnit)
            .Include(x => x.DefaultProduct).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw Missing("PLC device mapping");
        var tags = await db.OpcUaTagMappings.AsNoTracking().Where(x => x.PlcDeviceMappingId == id)
            .OrderBy(x => x.Direction).ThenBy(x => x.TagRole)
            .Select(x => new PlcTagConfiguration(x.Id, x.TagRole.ToString(), x.Direction.ToString(), x.NodeId,
                x.DataType.ToString(), x.IsRequired)).ToArrayAsync(ct);
        var checkpoint = await db.CounterCheckpoints.AsNoTracking().Where(x => x.PlcDeviceMappingId == id)
            .Select(x => new { x.LastReadAt, x.LastTotalCount, x.LastDefectCount, Blocked = x.LastSampleKey == "!counter-discontinuity"
                || x.LastSampleKey != null && x.LastSampleKey.StartsWith(CounterCheckpointEntity.RecoveryPendingPrefix) }).SingleOrDefaultAsync(ct);
        var runtime = await db.PlcDeviceRuntimeStates.AsNoTracking().Where(x => x.PlcDeviceMappingId == id)
            .Select(x => new { x.ConnectivityState, x.LastDataAt }).SingleOrDefaultAsync(ct);
        return new(MapDevice(entity, tags.Count(x => x.Direction == nameof(TagDirectionEnum.Read)),
            tags.Count(x => x.Direction == nameof(TagDirectionEnum.Write)), checkpoint?.LastReadAt,
            (runtime?.ConnectivityState ?? ConnectivityStateEnum.NotConfigured).ToString()) with
            {
                LastReceivedAt = runtime?.LastDataAt, CounterDiscontinuityPending = checkpoint?.Blocked == true,
                LastTotalCounter = checkpoint?.LastTotalCount, LastDefectCounter = checkpoint?.LastDefectCount
            }, tags);
    }

    private static PlcDeviceMappingItem MapDevice(PlcDeviceMappingEntity x, int readTags, int writeTags,
        DateTimeOffset? lastReadAt, string connectivity) => new(
        x.Id, x.OpcUaConnectionId, x.OpcUaConnection.Code, x.WorkUnitId, x.WorkUnit.Code,
        x.DefaultProductId, x.DefaultProduct != null ? x.DefaultProduct.Code : null, x.ExternalDeviceId,
        x.CounterMode.ToString(), x.FaultCodeFormat.ToString(), x.ZeroFaultCodeMeansNoFault, x.DebounceSamples, x.Note,
        readTags, writeTags, lastReadAt, connectivity, x.IsActive, Convert.ToBase64String(x.RowVersion),
        WorkUnitName: x.WorkUnit.Name, DefaultProductName: x.DefaultProduct?.Name);

    /// <summary>Truy vấn fault-code mapping với scope theo WorkUnit hoặc global.</summary>
    public async Task<PagedResult<PlcFaultMappingItem>> FaultMappingsAsync(PlcListQuery query, CancellationToken ct)
    {
        var q = db.PlcFaultCodeMappings.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            q = q.Where(x => x.FaultCode.Contains(search) || x.DowntimeReason.Code.Contains(search)
                || x.DowntimeReason.Name.Contains(search) || (x.Description != null && x.Description.Contains(search)));
        }
        if (query.ScopeId.HasValue) q = q.Where(x => x.WorkUnitId == query.ScopeId);
        if (!string.IsNullOrWhiteSpace(query.State) && bool.TryParse(query.State, out var active)) q = q.Where(x => x.IsActive == active);
        q = (query.SortBy?.ToLowerInvariant(), query.Descending) switch
        {
            ("scope", false) => q.OrderBy(x => x.WorkUnitId == null).ThenBy(x => x.WorkUnit!.Code),
            ("scope", true) => q.OrderByDescending(x => x.WorkUnitId == null).ThenByDescending(x => x.WorkUnit!.Code),
            ("reason", false) => q.OrderBy(x => x.DowntimeReason.Code),
            ("reason", true) => q.OrderByDescending(x => x.DowntimeReason.Code),
            (_, true) => q.OrderByDescending(x => x.FaultCode),
            _ => q.OrderBy(x => x.FaultCode),
        };
        var total = await q.CountAsync(ct);
        var items = await q.Skip(Offset(query.Page, query.PageSize)).Take(PageSize(query.PageSize))
            .Select(x => new PlcFaultMappingItem(x.Id, x.WorkUnitId, x.WorkUnitId == null ? "Global" : x.WorkUnit!.Code,
                x.FaultCode, x.DowntimeReasonId, x.DowntimeReason.Code + " — " + x.DowntimeReason.Name,
                x.Description, x.IsActive, Convert.ToBase64String(x.RowVersion))).ToArrayAsync(ct);
        return new(items, total);
    }

    /// <summary>Lấy các danh mục đang dùng cho lookup trong dialog cấu hình PLC.</summary>
    /// <param name="ct">Tín hiệu hủy được truyền tới các truy vấn SQL bất đồng bộ.</param>
    /// <returns>Kết nối active, thiết bị, sản phẩm và lý do dừng dưới dạng DTO gọn.</returns>
    public async Task<PlcConfigurationLookups> LookupsAsync(CancellationToken ct) => new(
        await db.OpcUaConnections.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Code)
            .Select(x => new PlcLookup(x.Id, x.Code, x.Name)).ToArrayAsync(ct),
        await db.WorkUnits.AsNoTracking().OrderBy(x => x.Code)
            .Select(x => new PlcLookup(x.Id, x.Code, x.Name)).ToArrayAsync(ct),
        await db.Products.AsNoTracking().OrderBy(x => x.Code)
            .Select(x => new PlcLookup(x.Id, x.Code, x.Name)).ToArrayAsync(ct),
        await db.DowntimeReasons.AsNoTracking().OrderBy(x => x.Code)
            .Select(x => new PlcLookup(x.Id, x.Code, x.Name)).ToArrayAsync(ct));

    private static int PageSize(int value) => Math.Clamp(value, 1, 100);
    private static int Offset(int page, int pageSize) => (Math.Max(1, page) - 1) * PageSize(pageSize);
    private static KeyNotFoundException Missing(string name) => new($"{name} was not found.");
}
