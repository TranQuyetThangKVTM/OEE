using System.ComponentModel.DataAnnotations;

namespace Oee.Application.Contracts.Api;

/// <summary>Query contract phân trang dùng chung cho API v1; Page bắt đầu từ 1 và PageSize bị giới hạn 100.</summary>
public sealed record PageRequest(
    [property: Range(1, int.MaxValue)] int Page = 1,
    [property: Range(1, 100)] int PageSize = 25,
    string? Search = null,
    string? Sort = null,
    bool Descending = false);
