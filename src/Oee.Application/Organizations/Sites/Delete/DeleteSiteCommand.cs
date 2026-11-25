using Oee.Application.Common;

namespace Oee.Application.Organizations.Sites.Delete;

/// <summary>Dữ liệu yêu cầu cho thao tác DeleteSite.</summary>
public sealed record DeleteSiteCommand(
    Guid Id,
    string RowVersion) : ICommand;
