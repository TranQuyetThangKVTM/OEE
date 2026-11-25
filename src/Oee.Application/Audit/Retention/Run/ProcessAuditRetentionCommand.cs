using Oee.Application.Common;

namespace Oee.Application.Audit.Retention.Run;

/// <summary>Dữ liệu yêu cầu cho thao tác ProcessAuditRetention.</summary>
public sealed record ProcessAuditRetentionCommand() : ICommand<(int DeletedCount, DateTimeOffset Cutoff)>;
