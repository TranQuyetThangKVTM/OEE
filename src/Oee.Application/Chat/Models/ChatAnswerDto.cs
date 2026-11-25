namespace Oee.Application.Chat;

public sealed record ChatAnswerDto(
    string Text,
    IReadOnlyList<ChatCitation> Citations,
    IReadOnlyList<string> FollowUpSuggestions,
    ChatIntentKind ResolvedIntent);
