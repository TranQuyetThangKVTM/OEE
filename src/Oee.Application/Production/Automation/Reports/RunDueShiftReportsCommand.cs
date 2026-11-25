using Oee.Application.Common;

namespace Oee.Application.Production.Automation;

/// <summary>Host nền xuất các ca đã kết thúc từ lúc bật lịch; không nhận ngày/ca tùy ý từ client.</summary>
public sealed record RunDueShiftReportsCommand : ICommand<int>;
