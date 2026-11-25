namespace Oee.Application.Chat;

public sealed record ChatTimeRange(
    DateOnly From,
    DateOnly To,
    string Label)
{
    public bool IsSingleDay => From == To;
}
