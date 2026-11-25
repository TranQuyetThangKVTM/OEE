using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Oee.Domain.PlcIntegration;

namespace Oee.Infrastructure.Persistence.Configurations;

/// <summary>Ánh xạ cấu hình connection OPC UA; credential chỉ lưu dưới dạng secret reference.</summary>
public sealed class OpcUaConnectionConfiguration : IEntityTypeConfiguration<OpcUaConnectionEntity>
{
    public void Configure(EntityTypeBuilder<OpcUaConnectionEntity> b)
    {
        b.ToTable("OpcUaConnections", "plc");
        b.ConfigureSoftDeleted();
        b.Property(x => x.Code).HasMaxLength(32).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.EndpointUrl).HasMaxLength(500).IsRequired();
        b.Property(x => x.SecurityMode).HasMaxLength(64).IsRequired();
        b.Property(x => x.SecurityPolicy).HasMaxLength(200).IsRequired();
        b.Property(x => x.AuthenticationType).HasMaxLength(32).IsRequired();
        b.Property(x => x.UserName).HasMaxLength(256);
        b.Property(x => x.CredentialSecretReference).HasMaxLength(500);
        b.Property(x => x.CertificateReference).HasMaxLength(500);
        b.HasIndex(x => x.Code).IsUnique().HasFilter("[IsDeleted] = 0");
    }
}
