using Oee.Application.Common;

namespace Oee.Application.Production.Automation;

/// <summary>Chỉ được gọi bởi host nền; dữ liệu ngày/mẫu được lấy từ lịch đã lưu, không nhận từ client.</summary>
public sealed record RunDueProductionPlanScheduleCommand : ICommand<PlanAutomationResult?>;
