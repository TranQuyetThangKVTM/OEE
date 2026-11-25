using System.ComponentModel.DataAnnotations;

namespace Oee.Application.Contracts.Api;

/// <summary>Request resolve cảnh báo có optimistic concurrency.</summary>
public sealed record AlertResolveRequest(
    [property: Required] string ConcurrencyToken,
    [property: StringLength(500)] string? Note);
