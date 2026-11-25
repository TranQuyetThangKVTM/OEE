using Oee.Application.Common;
using Oee.Application.Contracts.Operations;
using Oee.Application.Production.Plans.Common;
using Oee.Application.Security;

namespace Oee.Application.Production.Automation;

internal sealed class PlanAutomationContext(
    IProductionPlanScheduleStore schedules,
    ProductionPlanContext plans,
    IClock clock,
    ICurrentActor actor,
    ICapabilityChecker? capabilities = null)
    : IUseCaseContext
{
    internal IProductionPlanScheduleStore Schedules => schedules;
    internal ProductionPlanContext Plans => plans;
    internal DateTimeOffset Now => clock.UtcNow;
    internal ICurrentActor Actor => actor;

    internal void EnsureManage()
    {
        // Không coi việc thiếu checker/HttpContext là một quyền quản lý lịch.
        if (capabilities?.HasCapability(Capabilities.ProductionPlanManage) != true)
            throw new UnauthorizedAccessException("Bạn không có quyền quản lý kế hoạch sản xuất.");
    }

    internal async Task<(string Code, TimeZoneInfo Zone)> TemplateAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var sample = await plans.Store.FindSampleAsync(id, cancellationToken)
            ?? throw new ArgumentException("Kế hoạch mẫu không tồn tại hoặc đã bị xóa.");
        var items = await plans.Store.ReadSampleItemsAsync(id, cancellationToken);

        if (items.Length == 0)
            throw new ArgumentException("Kế hoạch mẫu chưa có dòng thiết bị × ca × SKU.");
        var sites = items.Select(item => item.Shift.SiteId).Distinct().ToArray();

        if (sites.Length != 1)
            throw new ArgumentException("Lịch tự động yêu cầu mẫu thuộc một nhà máy để xác định đúng ngày và múi giờ.");
        TimeZoneInfo zone;

        try
        {
            zone = TimeZoneInfo.FindSystemTimeZoneById(items[0].Shift.Site.TimeZoneId);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new ArgumentException("Múi giờ nhà máy của kế hoạch mẫu không hợp lệ.", exception);
        }

        return (sample.Code, zone);
    }

    internal static BulkProductionPlanRequest Request(Guid sampleId, DateOnly date)
        =>
        new(BulkPlanCreationModeContract.FromSample, [], [], null, sampleId, date, null, null);
}
