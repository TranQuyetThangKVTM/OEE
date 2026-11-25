namespace Oee.Application.Production.Automation;

/// <summary>Khóa lịch xuyên process đến khi hoàn tất đọc/ghi và tạo kế hoạch.</summary>
public interface IProductionPlanScheduleStore
{
    bool Exists
    {
        get;
    }

    Task<IProductionPlanScheduleSession> OpenAsync(CancellationToken ct);
}
