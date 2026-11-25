
namespace Oee.Infrastructure.Operations;

internal sealed record ProductionModuleSampleRow(Guid Id, string Code, string Name, string? Description, int ItemCount, int WorkUnitCount,
    int ShiftCount, decimal TotalQuantity, DateTimeOffset UpdatedAt, byte[] RowVersion, string? UpdatedBy);
