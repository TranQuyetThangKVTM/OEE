using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Oee.Domain.Downtime;
using Oee.Domain.Entities.Oee;
using Oee.Domain.Entities.Organization;
using Oee.Domain.Entities.Production;
using Oee.Domain.Platform;
using Oee.Domain.PlcIntegration;
using Oee.Infrastructure.Identity;

namespace Oee.Infrastructure.Persistence;


public sealed class OeeDbContext(DbContextOptions<OeeDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<EnterpriseEntity> Enterprises => Set<EnterpriseEntity>();
    public DbSet<SiteEntity> Sites => Set<SiteEntity>();
    public DbSet<AreaEntity> Areas => Set<AreaEntity>();
    public DbSet<WorkCenterEntity> WorkCenters => Set<WorkCenterEntity>();
    public DbSet<WorkUnitEntity> WorkUnits => Set<WorkUnitEntity>();
    public DbSet<ShiftEntity> Shifts => Set<ShiftEntity>();
    public DbSet<ProductEntity> Products => Set<ProductEntity>();
    public DbSet<WorkUnitStandardEntity> WorkUnitStandards => Set<WorkUnitStandardEntity>();
    public DbSet<DowntimeReasonEntity> DowntimeReasons => Set<DowntimeReasonEntity>();
    public DbSet<ProductionPlanEntity> ProductionPlans => Set<ProductionPlanEntity>();
    public DbSet<ProductionOutputEntity> ProductionOutputs => Set<ProductionOutputEntity>();
    public DbSet<ProductionReconciliationItemEntity> ProductionReconciliationItems => Set<ProductionReconciliationItemEntity>();
    public DbSet<SampleProductionPlanEntity> SampleProductionPlans => Set<SampleProductionPlanEntity>();
    public DbSet<SampleProductionPlanItemEntity> SampleProductionPlanItems => Set<SampleProductionPlanItemEntity>();
    public DbSet<DowntimeEventEntity> DowntimeEvents => Set<DowntimeEventEntity>();
    public DbSet<OeeSnapshotEntity> OeeSnapshots => Set<OeeSnapshotEntity>();
    public DbSet<OeeTargetEntity> OeeTargets => Set<OeeTargetEntity>();
    public DbSet<OeeAlertEntity> OeeAlerts => Set<OeeAlertEntity>();
    public DbSet<OpcUaConnectionEntity> OpcUaConnections => Set<OpcUaConnectionEntity>();
    public DbSet<PlcDeviceMappingEntity> PlcDeviceMappings => Set<PlcDeviceMappingEntity>();
    public DbSet<OpcUaTagMappingEntity> OpcUaTagMappings => Set<OpcUaTagMappingEntity>();
    public DbSet<PlcDeviceRuntimeStateEntity> PlcDeviceRuntimeStates => Set<PlcDeviceRuntimeStateEntity>();
    public DbSet<CounterCheckpointEntity> CounterCheckpoints => Set<CounterCheckpointEntity>();
    public DbSet<PlcFaultCodeMappingEntity> PlcFaultCodeMappings => Set<PlcFaultCodeMappingEntity>();
    public DbSet<PlcIntegrationEventEntity> PlcIntegrationEvents => Set<PlcIntegrationEventEntity>();
    public DbSet<PlcSessionEventEntity> PlcSessionEvents => Set<PlcSessionEventEntity>();
    public DbSet<AuditLogEntity> AuditLogs => Set<AuditLogEntity>();
    public DbSet<AuditRetentionRunEntity> AuditRetentionRuns => Set<AuditRetentionRunEntity>();
    public DbSet<OutboxMessageEntity> OutboxMessages => Set<OutboxMessageEntity>();

    /// <summary>Lưu đồng bộ sau khi kiểm tra append-only và chuẩn hóa UTC.</summary>
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        NormalizeUtcOffsets();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    /// <summary>Lưu bất đồng bộ; CancellationToken được truyền tới EF/SQL Server.</summary>
    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        NormalizeUtcOffsets();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>Áp dụng mọi IEntityTypeConfiguration và đổi tên bảng Identity theo schema hiện tại.</summary>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        // IdentityDbContext phải cấu hình model gốc trước khi ứng dụng bổ sung mapping riêng.
        base.OnModelCreating(builder);
        // EF quét assembly Infrastructure để tìm configuration, tránh một method cấu hình khổng lồ.
        builder.ApplyConfigurationsFromAssembly(typeof(OeeDbContext).Assembly);

        // Identity vẫn dùng entity chuẩn, nhưng bảng được đặt tên ngắn tương thích database OEE.
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.ToTable("Users", "identity");
            entity.Property(user => user.DisplayName).HasMaxLength(120);
        });
        builder.Entity<IdentityRole<Guid>>().ToTable("Roles", "identity");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles", "identity");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims", "identity");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins", "identity");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims", "identity");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens", "identity");
    }

    /// <summary>Bảo vệ audit và chuyển mọi DateTimeOffset thay đổi về offset +00:00.</summary>
    private void NormalizeUtcOffsets()
    {
        // Audit chỉ được thêm. Retention dùng luồng maintenance riêng, không update/delete qua ChangeTracker.
        var forbiddenAuditMutation = ChangeTracker.Entries<AuditLogEntity>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted);
        if (forbiddenAuditMutation)
        {
            throw new InvalidOperationException("AuditLog is append-only and cannot be updated or deleted through OeeDbContext.");
        }

        // Chỉ duyệt entity sắp ghi; giá trị lịch sử không bị tải và sửa lại.
        foreach (var entry in ChangeTracker.Entries().Where(entry => entry.State is EntityState.Added or EntityState.Modified))
        {
            if (entry.State == EntityState.Added && entry.Entity is ProductionPlanEntity)
            {
                // Điền cột NOT NULL của database cũ; nghiệp vụ mới không dùng trạng thái phát hành.
                entry.Property("Status").CurrentValue = 2;
            }
            foreach (var property in entry.Properties)
            {
                if (property.CurrentValue is DateTimeOffset value)
                {
                    property.CurrentValue = value.ToUniversalTime();
                }
            }
        }
    }
}
