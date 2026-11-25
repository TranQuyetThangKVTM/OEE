namespace Oee.Application.Contracts.Modules;

/// <summary>Dòng WorkUnit gồm cả cây tổ chức và hai trạng thái operational/connectivity riêng biệt.</summary>
public sealed record WorkUnitListItem(
    Guid Id,
    Guid WorkCenterId,
    string WorkCenter,
    string Area,
    string Site,
    string Code,
    string Name,
    string OperationalState,
    string ConnectivityState,
    string Status,
    DateTimeOffset UpdatedAt,
    string RowVersion,
    string? Description = null);
