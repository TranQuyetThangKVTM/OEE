using Oee.Domain.Common;

namespace Oee.Domain.Entities.Organization;

/// <summary>Master lý do dừng máy, gắn category OEE và kind chi tiết cho downtime mới.</summary>
/// <remarks>Đổi master không tự tái phân loại event lịch sử; reclassification là use case riêng có audit.</remarks>
public sealed class DowntimeReasonEntity : SoftDeletableAuditedEntity, IStatusEntity
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public DowntimeCategoryEnum Category { get; private set; }
    public DowntimeKindEnum Kind { get; private set; }
    public string? Description { get; private set; }
    public RecordStatusEnum Status { get; private set; } = RecordStatusEnum.Active;

    private DowntimeReasonEntity()
    {
    }

    public static DowntimeReasonEntity Create(string code, string name, DowntimeCategoryEnum category, DowntimeKindEnum kind,
        string? description = null, RecordStatusEnum status = RecordStatusEnum.Active)
    {
        var normalizedCode = DomainGuard.Required(code, 32);
        EnsureClassificationAllowed(normalizedCode, category, kind);
        return new DowntimeReasonEntity
        {
            Code = normalizedCode,
            Name = DomainGuard.Required(name, 200),
            Category = category,
            Kind = kind,
            Description = DomainGuard.Optional(description, 500),
            Status = ValidateStatus(normalizedCode, status)
        };
    }

    public void Update(string name, DowntimeCategoryEnum category, DowntimeKindEnum kind, string? description)
    {
        var normalizedName = DomainGuard.Required(name, 200);
        var normalizedDescription = DomainGuard.Optional(description, 500);
        EnsureClassificationAllowed(Code, category, kind);
        Name = normalizedName;
        Category = category;
        Kind = kind;
        Description = normalizedDescription;
    }

    public void ChangeStatus(RecordStatusEnum status) => Status = ValidateStatus(Code, status);

    public static void ValidateClassification(DowntimeCategoryEnum category, DowntimeKindEnum kind)
    {
        var valid = category switch
        {
            DowntimeCategoryEnum.Unknown => kind == DowntimeKindEnum.Unknown,
            DowntimeCategoryEnum.AvailabilityLoss => kind is DowntimeKindEnum.Planned or DowntimeKindEnum.Failure or DowntimeKindEnum.Breakdown or DowntimeKindEnum.MinorStop,
            DowntimeCategoryEnum.PerformanceLoss => kind == DowntimeKindEnum.MinorStop,
            DowntimeCategoryEnum.QualityLoss => kind == DowntimeKindEnum.Quality,
            _ => false
        };
        if (!valid) throw new ArgumentException("Downtime category and detail kind are not a valid combination.");
    }

    private static void EnsureClassificationAllowed(string code, DowntimeCategoryEnum category, DowntimeKindEnum kind)
    {
        ValidateClassification(category, kind);
        if (code == "DR-UNKNOWN" && (category != DowntimeCategoryEnum.Unknown || kind != DowntimeKindEnum.Unknown))
            throw new InvalidOperationException("DR-UNKNOWN must keep Unknown category and kind.");
    }

    private static RecordStatusEnum ValidateStatus(string code, RecordStatusEnum status)
    {
        DomainGuard.Defined(status);
        if (code == "DR-UNKNOWN" && status != RecordStatusEnum.Active)
            throw new InvalidOperationException("DR-UNKNOWN is a protected active system reason.");
        return status;
    }
}
