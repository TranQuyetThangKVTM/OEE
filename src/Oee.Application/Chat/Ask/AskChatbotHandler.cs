using Oee.Application.Common;

namespace Oee.Application.Chat;

internal sealed class AskChatbotHandler(ChatContext context) : IRequestHandler<AskChatbotQuery, ChatAnswerDto>
{
    public async Task<ChatAnswerDto> HandleAsync(
        AskChatbotQuery request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(request.Question) || request.Question.Length > 1000)
            throw new ArgumentException("Câu hỏi cần từ 1 đến 1000 ký tự.");

        if (ChatAutomationQuestions.Matches(request.Question))
            return ChatAutomationQuestions.Answer();
        var preliminary = ChatIntentParser.Parse(request.Question, DateOnly.FromDateTime(context.Now.UtcDateTime));

        if (preliminary.Kind is ChatIntentKind.Help or ChatIntentKind.Unknown or ChatIntentKind.HowTo)
            return ChatGuidance.Answer(preliminary);

        var filters = await context.Reports.GetFilterOptionsAsync(cancellationToken);
        var intent = ChatIntentParser.Parse(request.Question, filters.Today);
        var unit = ChatContext.ResolveUnit(intent.Slots.WorkUnitCodeA, filters);
        var second = ChatContext.ResolveUnit(intent.Slots.WorkUnitCodeB, filters);
        var zone = unit?.Detail ?? filters.Sites.FirstOrDefault()?.Detail ?? "Asia/Ho_Chi_Minh";
        var currentQuestion = ChatIntentParser.Normalize(request.Question);

        if (intent.Kind is ChatIntentKind.LiveFleetStatus or ChatIntentKind.PlcHealthStatus
        or ChatIntentKind.CurrentShiftRemaining or ChatIntentKind.LongRunningDowntimes
            || intent.Kind == ChatIntentKind.WorkUnitDetail
            && (currentQuestion.Contains("hien tai", StringComparison.Ordinal)
            || currentQuestion.Contains("trang thai", StringComparison.Ordinal)
            || currentQuestion.Contains("dang chay", StringComparison.Ordinal)))
            return await new ChatLiveAnswers(context).AnswerAsync(intent, unit, zone, filters, cancellationToken);

        if (intent.Kind is ChatIntentKind.PlanAttainment or ChatIntentKind.ActivePlans or ChatIntentKind.NextPlan
        or ChatIntentKind.OpenAlerts or ChatIntentKind.AlertStatistics
        or ChatIntentKind.OeeThresholds or ChatIntentKind.OeeTargetsList or ChatIntentKind.DeviceMappings
        or ChatIntentKind.PlcFaultCodeMappings or ChatIntentKind.DowntimeReasonsList or ChatIntentKind.OrgStructure
        or ChatIntentKind.MasterDataList or ChatIntentKind.AuditLogOverview)
            return await new ChatOperationsAnswers(context).AnswerAsync(intent, unit, zone, cancellationToken);

        return await new ChatReportAnswers(context).AnswerAsync(intent, unit, second, filters, cancellationToken);
    }
}
