using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oee.Domain.Entities.Organization;

namespace Oee.Infrastructure.Persistence.Configurations;

/// <summary>Ánh xạ Product sang bảng Products và bảo vệ business code active duy nhất.</summary>
public sealed class ProductConfiguration : IEntityTypeConfiguration<ProductEntity>
{
    public void Configure(EntityTypeBuilder<ProductEntity> b)
    {
        b.ToTable("Products", "catalog");
        b.ConfigureSoftDeleted();
        b.ConfigureStatus();
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.DefaultUom).HasMaxLength(16).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        // Giá trị mặc định thuộc Product/use case; schema SQL hiện hữu không có default constraint.
        b.Property(x => x.Status).IsRequired();
        b.HasIndex(x => x.Code).IsUnique().HasFilter("[IsDeleted] = 0");

    }
}
