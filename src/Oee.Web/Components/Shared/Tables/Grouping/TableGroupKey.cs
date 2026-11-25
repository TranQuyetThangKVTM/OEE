namespace Oee.Web.Components.Shared;

/// <summary>Khóa nhóm theo Id, kèm mã và tên để trình bày tiêu đề.</summary>
public sealed record TableGroupKey(object Id, string Code, string? Name = null);
