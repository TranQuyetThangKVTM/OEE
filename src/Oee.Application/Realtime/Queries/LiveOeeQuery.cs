namespace Oee.Application.Contracts.Modules;

/// <summary>Query live, đánh dấu stale khi source timestamp cũ hơn ngưỡng giây.</summary>
public sealed record LiveOeeQuery(int StaleAfterSeconds = 120);
