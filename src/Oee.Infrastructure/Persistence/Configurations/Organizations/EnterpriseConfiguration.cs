using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oee.Domain.Entities.Organization;

namespace Oee.Infrastructure.Persistence.Configurations;

/// <summary>Ánh xạ nút gốc Enterprise của cây tổ chức.</summary>
public sealed class EnterpriseConfiguration : IEntityTypeConfiguration<EnterpriseEntity>
{
    public void Configure(EntityTypeBuilder<EnterpriseEntity> b)
    {
        b.ToTable("Enterprises", "org");
        b.ConfigureSoftDeleted();
        b.ConfigureStatus();
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Description).HasMaxLength(500);
        b.HasIndex(x => x.Code).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
