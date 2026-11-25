namespace Oee.Infrastructure.Operations;

internal sealed record ProductionModulePlanRow(Guid Id, string Code, Guid WorkUnitId, string WorkUnit, Guid ProductId, string Product,
    Guid? ShiftId, string? Shift, DateTimeOffset Start, DateTimeOffset End, decimal Planned, decimal Actual,
    int Version, byte[] RowVersion, bool HasOutput, string WorkUnitName, string ProductName);
