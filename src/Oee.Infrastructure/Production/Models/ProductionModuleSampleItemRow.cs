
namespace Oee.Infrastructure.Operations;

internal sealed record ProductionModuleSampleItemRow(Guid Id, Guid WorkUnitId, string WorkUnit, Guid ProductId, string Product,
    Guid ShiftId, string Shift, decimal Quantity, string? Notes, byte[] RowVersion,
    string WorkUnitName, string ProductName, string ShiftName, TimeOnly ShiftStart, TimeOnly ShiftEnd);
