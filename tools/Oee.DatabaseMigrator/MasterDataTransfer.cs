using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Oee.Application.Common;
using Oee.Application.PlcIntegration;
using Oee.Domain.Common;
using Oee.Domain.Entities.Oee;
using Oee.Domain.Entities.Organization;
using Oee.Domain.PlcIntegration;
using Oee.Infrastructure.Persistence;

namespace Oee.DatabaseMigrator;

/// <summary>Xuất danh mục đang tồn tại; nhập giữ Id/FK vào database mới, không sao chép tài khoản hoặc lịch sử runtime.</summary>
public sealed class MasterDataTransfer(OeeDbContext db)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private sealed record Table(Type Type, Func<CancellationToken, Task<object[]>> Read);
    private Table[] Tables =>
    [
        Set<EnterpriseEntity>(), Set<SiteEntity>(), Set<AreaEntity>(), Set<WorkCenterEntity>(), Set<WorkUnitEntity>(),
        Set<ProductEntity>(), Set<ShiftEntity>(), Set<DowntimeReasonEntity>(), Set<WorkUnitStandardEntity>(), Set<OeeTargetEntity>(),
        Set<OpcUaConnectionEntity>(), Set<PlcDeviceMappingEntity>(), Set<OpcUaTagMappingEntity>(), Set<PlcFaultCodeMappingEntity>(),
    ];
    private Table Set<T>() where T : SoftDeletableAuditedEntity => new(typeof(T), async ct =>
        (await db.Set<T>().AsNoTracking().OrderBy(x => x.Id).ToArrayAsync(ct)).Cast<object>().ToArray());

    /// <summary>Snapshot nhất quán; chỉ scalar đã lưu, bỏ rowversion để SQL đích cấp token mới.</summary>
    public async Task ExportAsync(string path, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var tables = new Dictionary<string, JsonElement[]>(StringComparer.Ordinal);
        foreach (var table in Tables)
        {
            var properties = Properties(table.Type);
            tables.Add(EntityTypeName.Get(table.Type), (await table.Read(ct)).Select(entity => JsonSerializer.SerializeToElement(
                properties.ToDictionary(property => property.Name, property => property.PropertyInfo!.GetValue(entity), StringComparer.Ordinal), JsonOptions)).ToArray());
        }
        await transaction.CommitAsync(ct);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new MasterDataSnapshot(1, tables), JsonOptions), ct);
    }

    /// <summary>Không merge/ghi đè: từ chối nếu bất kỳ danh mục đích đã có dữ liệu. Import toàn bộ trong một transaction.</summary>
    public async Task ImportAsync(string path, CancellationToken ct)
    {
        var snapshot = JsonSerializer.Deserialize<MasterDataSnapshot>(await File.ReadAllTextAsync(path, ct), JsonOptions)
            ?? throw new InvalidOperationException("Master-data file is empty.");
        var tables = Tables;
        if (snapshot.FormatVersion != 1 || snapshot.Tables.Count != tables.Length
            || tables.Any(table => !snapshot.Tables.ContainsKey(EntityTypeName.Get(table.Type))))
            throw new InvalidOperationException("Master-data format/tables do not match this package.");
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            foreach (var table in tables)
                if ((await table.Read(ct)).Length > 0)
                    throw new InvalidOperationException("Import requires empty master-data tables; existing data will not be overwritten.");
            foreach (var table in tables)
            {
                var properties = Properties(table.Type);
                foreach (var row in snapshot.Tables[EntityTypeName.Get(table.Type)])
                {
                    if (row.ValueKind != JsonValueKind.Object || row.EnumerateObject().Count() != properties.Length)
                        throw new InvalidOperationException("Invalid scalar fields in master-data snapshot.");
                    var entity = Activator.CreateInstance(table.Type, nonPublic: true)!;
                    var entry = db.Entry(entity);
                    foreach (var property in properties)
                    {
                        if (!row.TryGetProperty(property.Name, out var value))
                            throw new InvalidOperationException("Missing scalar field in master-data snapshot.");
                        entry.Property(property.Name).CurrentValue = value.Deserialize(property.ClrType, JsonOptions);
                    }
                    if (entity is SoftDeletableAuditedEntity { IsDeleted: true } || ((Entity)entity).Id == Guid.Empty)
                        throw new InvalidOperationException("Deleted/empty-Id master row cannot be imported.");
                    entry.State = EntityState.Added;
                }
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            throw;
        }
    }

    /// <summary>Tạo kết nối mặc định để form thiết bị/tag dùng được ngay; endpoint vận hành đọc từ cấu hình chung.</summary>
    public async Task InitializeOpcUaAsync(OpcUaClientOptions options, CancellationToken ct)
    {
        if (!options.Enabled) return;
        if (!options.IsValid()) throw new InvalidOperationException("OpcUa configuration is invalid.");
        if (await db.OpcUaConnections.AnyAsync(ct)) return;
        if (options.Auth.Mode != "Anonymous")
            throw new InvalidOperationException("Initial OPC UA connection currently requires Anonymous; configure credentials separately.");
        var connection = OpcUaConnectionEntity.Create("OPC-DEFAULT", options.ApplicationName, options.EndpointUrl!,
            options.SecurityMode, options.SecurityPolicy);
        connection.RecordCreation(connection.CreatedAt, "FieldTestInitializer");
        db.Add(connection);
        await db.SaveChangesAsync(ct);
    }

    private IProperty[] Properties(Type type) => db.Model.FindEntityType(type)!.GetProperties()
        .Where(property => property.PropertyInfo is not null && property.ValueGenerated != ValueGenerated.OnAddOrUpdate)
        .OrderBy(property => property.Name, StringComparer.Ordinal).ToArray();
}

/// <summary>Danh mục allow-list; không chứa SQL credential, Identity secret, event hay counter checkpoint.</summary>
public sealed record MasterDataSnapshot(int FormatVersion, Dictionary<string, JsonElement[]> Tables);
