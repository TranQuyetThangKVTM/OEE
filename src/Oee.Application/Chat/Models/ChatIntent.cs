namespace Oee.Application.Chat;

public sealed record ChatIntent(
    ChatIntentKind Kind,
    ChatSlots Slots);
