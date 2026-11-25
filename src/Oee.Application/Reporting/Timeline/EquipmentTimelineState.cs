namespace Oee.Application.Contracts.Reporting;

public enum EquipmentTimelineState
{
    Unknown,
    Running,
    Failure,
    PlannedStop,
    OtherStop,
    Stopped,
    NoProduction,
    Future
}
