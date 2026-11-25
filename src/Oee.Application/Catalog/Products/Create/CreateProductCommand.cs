using Oee.Application.Common;
using Oee.Application.Contracts.Operations;

namespace Oee.Application.Catalog.Products.Create;

/// <summary>Dữ liệu yêu cầu cho thao tác CreateProduct.</summary>
public sealed record CreateProductCommand(
    string Code,
    string Name,
    string DefaultUom,
    string? Description,
    string Status
    )
    : ICommand<MasterMutationResult>;
