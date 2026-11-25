using System.ComponentModel.DataAnnotations;

namespace Oee.Application.Contracts.Api;

/// <summary>Request acknowledge có concurrency token và note giới hạn 500 ký tự.</summary>
public sealed record AlertAcknowledgeRequest(
    [property: Required] string ConcurrencyToken,
    [property: StringLength(500)] string? Note);
