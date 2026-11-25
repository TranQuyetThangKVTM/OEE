using Oee.Domain.Common;
using Oee.Domain.Entities.Organization;

namespace Oee.Domain.PlcIntegration;

/// <summary>Liên kết một thiết bị PLC bên ngoài với một <see cref="WorkUnit"/> OEE.</summary>
/// <remarks>
/// Giữ counter mode, fault format, debounce và quy ước fault 0. Runtime/checkpoint tần suất cao nằm ở entity riêng,
/// tránh làm nóng rowversion của cấu hình này.
/// </remarks>
public sealed class PlcDeviceMappingEntity : SoftDeletableAuditedEntity
{
    public Guid OpcUaConnectionId { get; private set; }
    public OpcUaConnectionEntity OpcUaConnection { get; private set; } = null!;
    public Guid WorkUnitId { get; private set; }
    public WorkUnitEntity WorkUnit { get; private set; } = null!;
    public Guid? DefaultProductId { get; private set; }
    public ProductEntity? DefaultProduct { get; private set; }
    public string ExternalDeviceId { get; private set; } = string.Empty;
    public CounterModeEnum CounterMode { get; private set; }
    public FaultCodeFormatEnum FaultCodeFormat { get; private set; }
    public bool IsActive { get; private set; }
    public bool ZeroFaultCodeMeansNoFault { get; private set; } = true;
    public int DebounceSamples { get; private set; } = 2;
    public string? Note { get; private set; }

    private PlcDeviceMappingEntity()
    {
    }

    public static PlcDeviceMappingEntity Create(Guid opcUaConnectionId, Guid workUnitId, string externalDeviceId, CounterModeEnum counterMode,
        FaultCodeFormatEnum faultCodeFormat, bool isActive = true, Guid? defaultProductId = null, int debounceSamples = 2,
        bool zeroFaultCodeMeansNoFault = true, string? note = null)
    {
        ValidateDebounceSamples(debounceSamples);
        return new PlcDeviceMappingEntity
        {
            OpcUaConnectionId = DomainGuard.RequiredId(opcUaConnectionId),
            WorkUnitId = DomainGuard.RequiredId(workUnitId),
            DefaultProductId = DomainGuard.OptionalId(defaultProductId),
            ExternalDeviceId = DomainGuard.Required(externalDeviceId, 128),
            CounterMode = DomainGuard.Defined(counterMode),
            FaultCodeFormat = DomainGuard.Defined(faultCodeFormat),
            IsActive = isActive,
            DebounceSamples = debounceSamples,
            ZeroFaultCodeMeansNoFault = zeroFaultCodeMeansNoFault,
            Note = DomainGuard.Optional(note, 500)
        };
    }

    public void Update(Guid opcUaConnectionId, Guid workUnitId, string externalDeviceId, CounterModeEnum counterMode,
        FaultCodeFormatEnum faultCodeFormat, bool isActive, Guid? defaultProductId, int debounceSamples,
        bool zeroFaultCodeMeansNoFault, string? note)
    {
        DomainGuard.RequiredId(opcUaConnectionId);
        DomainGuard.RequiredId(workUnitId);
        DomainGuard.OptionalId(defaultProductId);
        DomainGuard.Defined(counterMode);
        DomainGuard.Defined(faultCodeFormat);
        ValidateDebounceSamples(debounceSamples);
        var external = DomainGuard.Required(externalDeviceId, 128);
        var normalizedNote = DomainGuard.Optional(note, 500);
        OpcUaConnectionId = opcUaConnectionId;
        WorkUnitId = workUnitId;
        DefaultProductId = defaultProductId;
        ExternalDeviceId = external;
        CounterMode = counterMode;
        FaultCodeFormat = faultCodeFormat;
        IsActive = isActive;
        DebounceSamples = debounceSamples;
        ZeroFaultCodeMeansNoFault = zeroFaultCodeMeansNoFault;
        Note = normalizedNote;
    }

    public void Delete(DateTimeOffset at)
    {
        SoftDelete(at);
        IsActive = false;
    }

    public string? NormalizeFault(string? value)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized)) return null;
        var withoutPrefix = normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? normalized[2..] : normalized;
        string result = FaultCodeFormat switch
        {
            FaultCodeFormatEnum.Text => normalized,
            FaultCodeFormatEnum.Hex when long.TryParse(withoutPrefix, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var hex) => hex.ToString(System.Globalization.CultureInfo.InvariantCulture),
            FaultCodeFormatEnum.NumericDecimal when decimal.TryParse(normalized, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var number) => number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            FaultCodeFormatEnum.Auto when normalized.StartsWith("0x", StringComparison.OrdinalIgnoreCase) && long.TryParse(withoutPrefix, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var autoHex) => autoHex.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => normalized
        };
        return ZeroFaultCodeMeansNoFault && decimal.TryParse(result, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var numeric) && numeric == 0 ? null : result;
    }

    private static void ValidateDebounceSamples(int debounceSamples)
    {
        if (debounceSamples is < 1 or > 20) throw new ArgumentOutOfRangeException(nameof(debounceSamples));
    }
}
