using Oee.Application.PlcIntegration.Monitoring;
using Oee.Domain.Common;

namespace Oee.Web.Components.Pages.Plc;

public sealed record OpcTagMonitorRow(OpcTagMonitorMapping Mapping, OpcTagObservation? Observation, string Connection,
    DateTimeOffset At, int NoDataSeconds)
{
    public bool Input => Mapping.Direction == TagDirectionEnum.Read;
    public bool Stale => Observation is not null && At - Observation.ReceivedAt > TimeSpan.FromSeconds(NoDataSeconds);
    public bool Problem => Input && (!Mapping.Enabled || Observation is null || !Observation.IsGood || Stale
        || Connection is "Disconnected" or "Connecting");
    public bool Changed => !Problem && Observation?.ChangedAt is { } changed && At >= changed && At - changed <= TimeSpan.FromSeconds(5);
    public string Status => !Mapping.Enabled ? "Đang tắt" : !Input ? "Đầu ra · đọc trực tiếp"
        : Connection == "Disconnected" ? "Mất kết nối" : Connection == "Connecting" ? "Đang kết nối"
        : Observation is null ? "Chưa nhận dữ liệu" : !Observation.IsGood ? "Lỗi tag"
        : Stale ? "Chưa có dữ liệu mới" : "Good";
    public string RoleText => Mapping.Role switch
    {
        TagRoleEnum.Running => "RunState", TagRoleEnum.TotalCounter => "TotalCounter", TagRoleEnum.DefectCounter => "DefectCounter",
        TagRoleEnum.FaultCode => "FaultCode", TagRoleEnum.ProductCode => "ProductCode", TagRoleEnum.Oee => "OEE",
        TagRoleEnum.Availability => "Availability", TagRoleEnum.Performance => "Performance", TagRoleEnum.Quality => "Quality",
        TagRoleEnum.CurrentDowntime => "Downtime", TagRoleEnum.GoodQuantity => "Good", TagRoleEnum.TargetQuantity => "Target",
        TagRoleEnum.Mtbf => "MTBF", TagRoleEnum.Mttr => "MTTR", _ => Mapping.Role.ToString()
    };
}
