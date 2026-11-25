using Oee.Application.Common;

namespace Oee.Application.Catalog.Products.Delete;

/// <summary>Dữ liệu yêu cầu cho thao tác DeleteProduct.</summary>
public sealed record DeleteProductCommand(
    Guid Id,
    string RowVersion) : ICommand;
