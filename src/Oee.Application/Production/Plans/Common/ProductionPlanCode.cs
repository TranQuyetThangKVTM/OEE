namespace Oee.Application.Production.Plans.Common;

/// <summary>Mã kế hoạch theo SKU, ngày áp dụng, ca và tên máy; phù hợp cột Code hiện hữu.</summary>
public static class ProductionPlanCode
{
    public static string Format(
        string productCode,
        DateOnly appliedDate,
        string shiftCode,
        string machineName,
        string suffix = "")
    {
        var prefix = FormattableString.Invariant($"{productCode}-{appliedDate:yyyyMMdd}-{shiftCode}-");
        var machine = string.Concat(machineName.Where(character => !char.IsWhiteSpace(character)));
        var machineLength = 32 - prefix.Length - suffix.Length;
        if (machineLength < 1)
        {
            throw new InvalidOperationException("Mã SKU hoặc mã ca quá dài để tạo mã kế hoạch tối đa 32 ký tự. Hãy rút ngắn mã SKU hoặc mã ca.");
        }

        return prefix + machine[..Math.Min(machine.Length, machineLength)] + suffix;
    }
}
