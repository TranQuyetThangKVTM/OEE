using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Catalog.Products.Update;

/// <summary>Dữ liệu yêu cầu cho thao tác UpdateProduct.</summary>
public sealed record UpdateProductCommand(
    Guid Id,
    string Name,
    string DefaultUom,
    string? Description,
    string RowVersion
    )
    : ICommand<MasterMutationResult>;
