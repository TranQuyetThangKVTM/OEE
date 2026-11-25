namespace Oee.Application.Contracts.Modules;

public sealed record DashboardTimeTotals(
    decimal LoadingMinutes,
    decimal UtilizationMinutes,
    decimal IdealRunMinutes);
