using Oee.Application.Common;

namespace Oee.Application.Chat;

public sealed record AskChatbotQuery(string Question) : IQuery<ChatAnswerDto>;
