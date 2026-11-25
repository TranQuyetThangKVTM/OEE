using Oee.Application.Catalog.Products.Common;
using Oee.Domain.Common;
using static Oee.Application.Operations.MasterDataValidation;

namespace Oee.Application.Catalog.Products.Create;

/// <summary>Kiểm tra và chuẩn hóa đầu vào, không đọc hoặc ghi database.</summary>
internal static class CreateProductValidator
{
    internal static (
        string Code,
        string Name,
        string Uom,
        string? Description,
        RecordStatusEnum Status)
        ValidateProduct(CreateProductCommand command)
        => (
            Required(command.Code, 32, "Product code"),
            Required(command.Name, 200, "Product name"),
            CreateProductValidator.ValidateUom(command.DefaultUom),
            Optional(command.Description, 500, "Description"),
            Status(command.Status));

    internal static string ValidateUom(string value)
    {
        var uom = Required(value, 16, "Default UOM").ToUpperInvariant();

        if (!ProductContext.AllowedUoms.Contains(uom))
            throw new ArgumentException("Default UOM is not in the approved unit catalog.");

        return uom;
    }
}
