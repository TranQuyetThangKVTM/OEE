using Microsoft.EntityFrameworkCore;
using Oee.Application.PlcIntegration;
using Oee.Domain.Common;
using Oee.Infrastructure.Persistence;

namespace Oee.Infrastructure.PlcIntegration;

/// <summary>Query trạng thái OPC gọn cho global connection banner của Web.</summary>
public sealed class OpcUiStatusService(OeeDbContext db) : IOpcUiStatusService
{
    /// <summary>Tổng hợp số mapping, số connected và connectivity state xấu nhất.</summary>
    public async Task<OpcUiStatus> GetAsync(CancellationToken ct)
    {
        // Banner ở mọi trang: SQL chỉ trả một dòng tổng hợp, không tải cả danh sách runtime mỗi lần poll.
        var activeStates = from mapping in db.PlcDeviceMappings.AsNoTracking().Where(x => x.IsActive)
                           join runtime in db.PlcDeviceRuntimeStates.AsNoTracking()
                               on mapping.Id equals runtime.PlcDeviceMappingId into states
                           from runtime in states.DefaultIfEmpty()
                           select runtime == null ? (ConnectivityStateEnum?)null : runtime.ConnectivityState;
        var counts = await activeStates.GroupBy(_ => 1).Select(group => new
        {
            Configured = group.Count(),
            Connected = group.Count(x => x == ConnectivityStateEnum.Connected),
            BadTag = group.Count(x => x == ConnectivityStateEnum.BadTag),
            Disconnected = group.Count(x => x == ConnectivityStateEnum.Disconnected),
            NoData = group.Count(x => x == ConnectivityStateEnum.NoData)
        }).SingleOrDefaultAsync(ct);
        if (counts is null) return new(0, 0, 0, ConnectivityStateEnum.NotConfigured, DateTimeOffset.UtcNow);
        var worst = counts.BadTag > 0 ? ConnectivityStateEnum.BadTag
            : counts.Disconnected > 0 ? ConnectivityStateEnum.Disconnected
            : counts.NoData > 0 ? ConnectivityStateEnum.NoData : ConnectivityStateEnum.Connected;
        return new(counts.Configured, counts.Connected, counts.BadTag + counts.Disconnected + counts.NoData,
            worst, DateTimeOffset.UtcNow);
    }
}
