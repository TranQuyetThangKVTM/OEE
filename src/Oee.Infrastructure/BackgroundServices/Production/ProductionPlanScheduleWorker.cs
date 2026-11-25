using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Oee.Application.Common;
using Oee.Application.Production.Automation;

namespace Oee.Infrastructure.BackgroundServices;

/// <summary>Chỉ đánh thức use case mỗi phút; không phụ thuộc chat, browser hoặc phiên Remote Desktop.</summary>
public sealed partial class ProductionPlanScheduleWorker(IServiceScopeFactory scopes,
    ILogger<ProductionPlanScheduleWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<IUseCaseExecutor>()
                    .ExecuteAsync(new RunDueProductionPlanScheduleCommand(), stoppingToken);
                if (result is not null)
                    LogCompleted(logger, result.PlanDate, result.Created, result.Skipped, result.Conflicts, result.Invalid);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { LogFailed(logger, exception); }
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var count = await scope.ServiceProvider.GetRequiredService<IUseCaseExecutor>()
                    .ExecuteAsync(new RunDueShiftReportsCommand(), stoppingToken);
                if (count > 0) LogReports(logger, count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { LogReportFailure(logger, exception); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Automatic plans for {PlanDate}: created {Created}, skipped {Skipped}, conflicts {Conflicts}, invalid {Invalid}")]
    private static partial void LogCompleted(ILogger logger, DateOnly planDate, int created, int skipped, int conflicts, int invalid);
    [LoggerMessage(Level = LogLevel.Error, Message = "Automatic production plan tick failed; the worker will retry")]
    private static partial void LogFailed(ILogger logger, Exception exception);
    [LoggerMessage(Level = LogLevel.Information, Message = "Generated {Count} automatic shift PDF reports")]
    private static partial void LogReports(ILogger logger, int count);
    [LoggerMessage(Level = LogLevel.Error, Message = "Automatic shift PDF export failed; the worker will retry")]
    private static partial void LogReportFailure(ILogger logger, Exception exception);
}
