
namespace Oee.Infrastructure.Operations;

internal sealed record DashboardLossEventRow(string ReasonCode, string Reason, string Category, DateTimeOffset StartAt, DateTimeOffset? EndAt);
