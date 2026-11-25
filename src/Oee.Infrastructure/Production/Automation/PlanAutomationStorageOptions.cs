namespace Oee.Infrastructure.Production.Automation;

public sealed class PlanAutomationStorageOptions
{
    public const string SectionName = "PlanAutomation";
    /// <summary>Mặc định dưới ContentRoot, ngoài wwwroot. Có thể chọn thư mục dữ liệu riêng khi triển khai.</summary>
    public string StoragePath { get; set; } = "App_Data/ProductionPlanning";
}
