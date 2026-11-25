using Oee.Application.Common;
using Oee.Application.Security;

namespace Oee.Application.Production.LineOff;

internal sealed class LineOffContext(
    ILineOffCalendarStore calendar,
    ILineOffScopeStore scopes,
    IClock clock,
    ICurrentActor actor,
    ICapabilityChecker? capabilities = null) : IUseCaseContext
{
    internal ILineOffCalendarStore Calendar => calendar;
    internal ILineOffScopeStore Scopes => scopes;
    internal DateTimeOffset Now => clock.UtcNow;
    internal string UserName => actor.UserName;

    internal void EnsureManage()
    {
        if (capabilities?.HasCapability(Capabilities.ProductionPlanManage) != true)
            throw new UnauthorizedAccessException("Bạn không có quyền quản lý thời gian Off của Line.");
    }

    internal static void EnsureRevision(LineOffCalendar calendar, Guid expected)
    {
        if (calendar.Revision != expected)
            throw new ArgumentException("Lịch Off đã được người khác thay đổi. Hãy tải lại trước khi lưu.");
    }
}
