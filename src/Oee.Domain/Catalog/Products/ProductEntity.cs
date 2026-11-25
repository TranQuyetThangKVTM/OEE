using Oee.Domain.Common;

namespace Oee.Domain.Entities.Organization;

/// <summary>Master sản phẩm/SKU được dùng trong plan, output, standard và báo cáo.</summary>
/// <remarks>Mã là business key; trạng thái Inactive ngăn lựa chọn mới nhưng không xóa dữ liệu lịch sử.</remarks>
public sealed class ProductEntity : SoftDeletableAuditedEntity, IStatusEntity
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string DefaultUom { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public RecordStatusEnum Status { get; private set; } = RecordStatusEnum.Active;

    private ProductEntity()
    {
    }

    public static ProductEntity Create(string code, string name, string defaultUom, string? description = null, RecordStatusEnum status = RecordStatusEnum.Active)
    {
        return new ProductEntity
        {
            Code = DomainGuard.Required(code, 32),
            Name = DomainGuard.Required(name, 200),
            DefaultUom = DomainGuard.Required(defaultUom, 16),
            Description = DomainGuard.Optional(description, 500),
            Status = DomainGuard.Defined(status)
        };
    }

    public void Update(string name, string defaultUom, string? description)
    {
        var normalizedName = DomainGuard.Required(name, 200);
        var normalizedUom = DomainGuard.Required(defaultUom, 16);
        var normalizedDescription = DomainGuard.Optional(description, 500);
        Name = normalizedName;
        DefaultUom = normalizedUom;
        Description = normalizedDescription;
    }

    public void ChangeStatus(RecordStatusEnum status) => Status = DomainGuard.Defined(status);
}
