using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Oee.Domain.Common;
using Oee.Domain.Entities.Organization;
using Oee.Infrastructure.Persistence;

namespace Oee.Infrastructure.IntegrationTests;

public sealed class PersistenceModelTests
{
    [Theory]
    [InlineData(typeof(EnterpriseEntity))]
    [InlineData(typeof(SiteEntity))]
    [InlineData(typeof(AreaEntity))]
    [InlineData(typeof(WorkCenterEntity))]
    [InlineData(typeof(WorkUnitEntity))]
    [InlineData(typeof(ShiftEntity))]
    [InlineData(typeof(ProductEntity))]
    [InlineData(typeof(DowntimeReasonEntity))]
    public void MasterStatusReadsAndWritesExistingIntegerValues(Type entityType)
    {
        using var db = new OeeDbContext(new DbContextOptionsBuilder<OeeDbContext>()
            .UseSqlServer("Server=localhost;Database=SchemaComparisonOnly;Integrated Security=true").Options);
        var property = db.Model.FindEntityType(entityType)!.FindProperty(nameof(IStatusEntity.Status))!;
        var converter = property.GetTypeMapping().Converter!;

        Assert.Equal(typeof(RecordStatusEnum), property.ClrType);
        Assert.Equal("int", property.GetColumnType());
        Assert.Equal(typeof(int), converter.ProviderClrType);
        Assert.Equal(0, converter.ConvertToProvider(RecordStatusEnum.Inactive));
        Assert.Equal(1, converter.ConvertToProvider(RecordStatusEnum.Active));
        Assert.Equal(RecordStatusEnum.Inactive, converter.ConvertFromProvider(0));
        Assert.Equal(RecordStatusEnum.Active, converter.ConvertFromProvider(1));
    }

    [Fact]
    public void EnumStatusFiltersTranslateToExistingIntegerPredicates()
    {
        using var db = new OeeDbContext(new DbContextOptionsBuilder<OeeDbContext>()
            .UseSqlServer("Server=localhost;Database=SchemaComparisonOnly;Integrated Security=true").Options);

        var active = db.Enterprises.Where(entity => entity.Status == RecordStatusEnum.Active).ToQueryString();
        var inactive = db.Products.Where(entity => entity.Status == RecordStatusEnum.Inactive).ToQueryString();

        Assert.Contains("[Status] = 1", active, StringComparison.Ordinal);
        Assert.Contains("[Status] = 0", inactive, StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentModelKeepsExistingSqlSchema()
    {
        // Chỉ so metadata và sinh SQL chẩn đoán trong bộ nhớ; không mở kết nối database.
        using var db = new OeeDbContext(new DbContextOptionsBuilder<OeeDbContext>()
            .UseSqlServer("Server=localhost;Database=SchemaComparisonOnly;Integrated Security=true").Options);
        var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot;
        Assert.NotNull(snapshot);
        var previous = db.GetService<IModelRuntimeInitializer>().Initialize(snapshot.Model, designTime: true);
        var current = db.GetService<IDesignTimeModel>().Model;
        var changes = db.GetService<IMigrationsModelDiffer>().GetDifferences(previous.GetRelationalModel(), current.GetRelationalModel());
        var diagnostic = string.Join(Environment.NewLine,
            db.GetService<IMigrationsSqlGenerator>().Generate(changes).Select(command => command.CommandText));
        Assert.True(changes.Count == 0, diagnostic);
    }
}
