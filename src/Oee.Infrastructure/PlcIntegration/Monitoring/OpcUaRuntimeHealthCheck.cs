using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Oee.Domain.Common;
using Oee.Infrastructure.Persistence;

namespace Oee.Infrastructure.PlcIntegration;

/// <summary>Health check tổng hợp runtime connectivity của các PLC device mapping active.</summary>
/// <remarks>
/// Health check tạo scope riêng vì DbContext không được giữ singleton. Không có cấu hình là Healthy;
/// có Disconnected/BadTag/NoData là Degraded, không đồng nhất các trạng thái này với máy Stopped.
/// </remarks>
public sealed class OpcUaRuntimeHealthCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    /// <summary>Đọc số device cấu hình và runtime state mới nhất từ SQL.</summary>
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<OeeDbContext>();
        var configured = await db.PlcDeviceMappings.CountAsync(x => x.IsActive, cancellationToken); if (configured == 0) return HealthCheckResult.Healthy("No active OPC UA devices are configured.");
        var unhealthy = await db.PlcDeviceRuntimeStates.CountAsync(x => x.ConnectivityState == ConnectivityStateEnum.Disconnected || x.ConnectivityState == ConnectivityStateEnum.BadTag || x.ConnectivityState == ConnectivityStateEnum.NoData, cancellationToken);
        return unhealthy == 0 ? HealthCheckResult.Healthy($"{configured} OPC UA devices configured.") : HealthCheckResult.Degraded($"{unhealthy} OPC UA devices are disconnected, bad-tag, or no-data.");
    }
}
