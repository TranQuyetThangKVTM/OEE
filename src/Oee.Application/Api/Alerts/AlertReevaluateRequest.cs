using System.ComponentModel.DataAnnotations;

namespace Oee.Application.Contracts.Api;

/// <summary>Request tính lại cảnh báo từ snapshot/target hiện tại.</summary>
public sealed record AlertReevaluateRequest([property: Required] string ConcurrencyToken);
