using Oee.Application.Chat;

namespace Oee.Web.Components.Chat;

public sealed record ChatMessage(bool IsUser, string Text, ChatAnswerDto? Answer = null);
