namespace Oee.Application.Chat;

/// <summary>Projection chỉ đọc bổ sung cho trợ lý; không trả entity, IQueryable hoặc secret.</summary>
public interface IChatReadStore
{
    Task<ChatCatalogPage> CatalogAsync(
        ChatCatalogKind kind,
        Guid? workUnitId,
        DateTimeOffset from,
        DateTimeOffset until,
        DateOnly effectiveOn,
        int take,
        CancellationToken ct);

    Task<ChatPlanSummary> PlansAsync(
        Guid? workUnitId,
        DateTimeOffset from,
        DateTimeOffset until,
        bool startsAfter,
        int take,
        CancellationToken ct);

    Task<ChatTargetPage> TargetsAsync(
        Guid? workUnitId,
        DateOnly effectiveOn,
        int take,
        CancellationToken ct,
        DateOnly? expiresUntil = null);
}
